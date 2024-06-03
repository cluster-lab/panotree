from pydantic import BaseModel
from pydantic.dataclasses import dataclass
from copy import deepcopy
from json import JSONEncoder
from typing import List, Optional, Iterator, Callable


from exploration.hoo import Node
import numpy as np


class Vector3f(BaseModel):
    x: float = 0.0
    y: float = 0.0
    z: float = 0.0

    @classmethod
    def from_array(cls, arr: List[float]) -> 'Vector3f':
        return Vector3f(x=arr[0], y=arr[1], z=arr[2])
    @property
    def elements(self) -> List[float]:
        return [self.x, self.y, self.z]

    def __add__(self, other):
        return Vector3f(x=self.x + other.x,y= self.y + other.y,z= self.z + other.z)

    def __sub__(self, other):
        return Vector3f(x=self.x - other.x,y= self.y - other.y,z= self.z - other.z)

    def __mul__(self, other):
        return Vector3f(x=self.x * other.x, y=self.y * other.y,z= self.z * other.z)

    def __truediv__(self, other):
        return Vector3f(x=self.x / other.x,y= self.y / other.y,z= self.z / other.z)

    def filter_np(self):
        """
        replace np.float32 and np.float64 with float
        """
        return Vector3f(x=float(self.x), y=float(self.y), z=float(self.z))


class Vector4f(BaseModel):
    x: float = 0.0
    y: float = 0.0
    z: float = 0.0
    w: float = 0.0

    @classmethod
    def from_array(cls, arr: List[float]) -> 'Vector4f':
        return Vector4f(x=arr[0], y=arr[1], z=arr[2], w=arr[3])

    def filter_np(self):
        """
        replace np.float32 and np.float64 with float
        """
        return Vector4f(x=float(self.x),y= float(self.y),z= float(self.z),w= float(self.w))


class Bounds(BaseModel):
    """
    バウンディングボックス
    """
    min: Vector3f
    max: Vector3f

    @property
    def center(self) -> Vector3f:
        return (self.min + self.max) / Vector3f(x=2, y=2, z=2)

    @property
    def size(self) -> Vector3f:
        return self.max - self.min


class CameraParameter(BaseModel):
    """
    カメラパラメータ
    カメラの位置と姿勢を表す
    directionとquaternionはどちらか一方を指定する
    """
    position: Vector3f
    direction: Optional[Vector3f] = None
    quaternion: Optional[Vector4f] = None
    fieldOfView: float = 60
    aspect: float = 0

    def filter_np(self):
        """
        replace np.float32 and np.float64 with float
        """
        return CameraParameter(position=self.position.filter_np(),
                               direction=self.direction.filter_np() if self.direction is not None else None,
                               quaternion=self.quaternion.filter_np() if self.quaternion is not None else None,
                               fieldOfView=float(self.fieldOfView),
                               aspect=float(self.aspect))


class RenderSceneRequest(BaseModel):
    cameraParameters: List[CameraParameter]


class BoundingBox(BaseModel):
    min: Vector3f
    max: Vector3f


class PhotoScoring(BaseModel):
    """
    撮影スポットのスコアとカメラパラメータ
    """
    cameraParameter: CameraParameter
    score: float

    def filter_np(self):
        """
        replace np.float32 and np.float64 with float
        """
        return PhotoScoring(cameraParameter=self.cameraParameter.filter_np(),
                            score=float(self.score))


class LeafGridNode(BaseModel):
    gridId: str
    nodeId: str
    position: Vector3f
    photoScorings: List[PhotoScoring]


class NodeViewModel(BaseModel):
    """
    レンダリングサーバー上での表示用のノードビューモデル
    """
    id: str
    branchId: str | int
    parentId: Optional[str] = None
    depth: int
    min: Vector3f
    max: Vector3f
    score: float
    b: Optional[float] = None
    photoScorings: List[PhotoScoring]
    leafGridNodes: Optional[List[LeafGridNode]] = None
    child_left: Optional['NodeViewModel'] = None
    child_right: Optional['NodeViewModel'] = None

    @property
    def bounds(self) -> Bounds:
        return Bounds(min=self.min, max=self.max)

    @property
    def size(self) -> Vector3f:
        return self.max - self.min

    @property
    def children(self) -> List['NodeViewModel']:
        ret = []
        if self.child_left is not None:
            ret.append(self.child_left)
        if self.child_right is not None:
            ret.append(self.child_right)
        return ret

    @classmethod
    def from_node(cls, node: Node, photo_scoring: List[PhotoScoring]):
        parent_id = node.parent.id if node.parent is not None else None
        bbox_min = Vector3f(x=node.minX, y=node.minY, z=node.minZ)
        bbox_max = Vector3f(x=node.maxX, y=node.maxY, z=node.maxZ)
        return NodeViewModel(id=node.id,
                             branchId=node.branch_id,
                             parentId=parent_id,
                             depth=node.depth,
                             min=bbox_min,
                             max=bbox_max,
                             score=float(node.value),
                             b=float(node.B),
                             photoScorings=photo_scoring)

    @classmethod
    def create_tree(cls, iterator: Iterator['NodeViewModel']) -> 'NodeViewModel':
        node_dict = {}
        for node in iterator:
            node_dict[node.id] = node
            if node.parentId is not None and node.parentId in node_dict:
                parent = node_dict[node.parentId]
                branch_id = int(node.branchId)
                if branch_id % 2 == 0:
                    parent.child_left = node
                else:
                    parent.child_right = node

        return node_dict["0000-00000001"]

    def copy_without_children(self) -> 'NodeViewModel':
        t_node = deepcopy(self)
        t_node.child_left = None
        t_node.child_right = None
        return t_node

    def traverse(self, f: Callable[['NodeViewModel'], bool]):
        def _traverse(node: 'NodeViewModel'):
            if f(node):
                for child in node.children:
                    _traverse(child)

        _traverse(self)


class CalculateWorldBoundingBoxResponse(BaseModel):
    bbox: BoundingBox



class UpdateNodesRequest(BaseModel):
    nodes: List[NodeViewModel]


class RendererConfig(BaseModel):
    textureSize: int


class UpdateConfigRequest(BaseModel):
    rendererConfig: RendererConfig


class VersionInfo(BaseModel):
    majorVersion: int
    minorVersion: int
    buildNumber: int
    revisionNumber: int

    def __str__(self):
        return f"{self.majorVersion}.{self.minorVersion}.{self.buildNumber}.{self.revisionNumber}"


class GetServerInfoResponse(BaseModel):
    version: str
    versionInfo: VersionInfo
    platform: str


class PostComputeFakePhotoPositionsResponse(BaseModel):
    positions: List[Vector3f]


class Scene(BaseModel):
    unitySceneName: str
    assetBundleUrl: str


class Resources(BaseModel):
    mainScene: Scene
    subScenes: Optional[List[Scene]]


class CustomJsonEncoder(JSONEncoder):
    def default(self, o):
        if isinstance(o, np.float32) or isinstance(o, np.float64):
            return float(o)
        return o.__dict__
