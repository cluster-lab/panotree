import gzip
import json
import zlib
from io import StringIO
from typing import List, TypeVar, Type

import numpy as np
import requests
from requests_toolbelt.multipart.decoder import MultipartDecoder

from render_server.render_api_data import CustomJsonEncoder, RenderSceneRequest, \
    CalculateWorldBoundingBoxResponse, UpdateNodesRequest, UpdateConfigRequest, GetServerInfoResponse, PostComputeFakePhotoPositionsResponse
from util.serialize_utils import decode_as_simple_namespace
from util.time_measure import TimeMeasure


def _assert_response(response):
    """
    httpステータスコードが200以外の場合、エラーを発生させる
    Raises an error if the HTTP status code is not 200
    """
    if response.status_code != 200:
        raise RuntimeError(f'Agent Server Error: {response.status_code} {response.content.decode("utf_8_sig")}')


T = TypeVar("T")


class RenderAPIClient:
    """
    Unity Render Serverに対してリクエストを送信するためのクライアント
    以下の機能を持つ
    * シーンのロード
    * シーンのレンダリング
    * シーンのバウンディングボックスの計算
    * 表示ノードの更新
    * 表示ノードのリセット)
    * サーバー情報の取得 (OS, プラットフォーム, レンダラーの設定など)
    * フェイク写真の位置計算 (教師データ作成時のみ使用)
    The client for sending requests to the Unity Render Server
    It has the following functions:
    * Load scene
    * Render scene
    * Calculate the bounding box of the scene
    * Update display nodes
    * Reset display nodes
    * Get server information (OS, platform, renderer settings, etc.)
    * Calculate the position of fake photos (used only when creating training data)
    """

    def __init__(self, endpoint_url: str):
        """

        Args:
            endpoint_url:
        """
        self.endpoint_url = endpoint_url
        self._json_encoder = CustomJsonEncoder()
        # レンダリング画像のサイズ。原則224固定で良い。
        self._texture_size = 224
        # リクエストのタイムアウト設定 [秒]
        self._kwargs = {
            "timeout": (5.0, 5.0)
        }
    def request_render(self, camera_parameters: RenderSceneRequest) -> List[np.ndarray]:
        """
        レンダリングサーバーに対して、指定されたカメラパラメータでシーンをレンダリングするようリクエストする
        Rquest the rendering server to render the scene with the specified camera parameters
        Args:
            camera_parameters: レンダリングするカメラパラメータ camera parameters to render
        Returns:
            6x6でタイリングされた画像のリスト。
            大きさは (self._texture_size * 6, self._texture_size * 6, 3) 規定値の場合は (1344, 1344, 3)
            1画素は3バイトのRGB値で表現される
            一度に36以上の画像をレンダリングさせると、返ってくる画像が2枚以上になる
            List of images tiled in 6x6.
            The size is (self._texture_size * 6, self._texture_size * 6, 3) default value is (1344, 1344, 3)
            One pixel is represented by a 3-byte RGB value
            If you render more than 36 images at once, you will get more than 2 images back
        """
        headers = {'Content-Type': 'application/json'}
        tm = TimeMeasure.default()

        request_body = self._encode_request_body(camera_parameters)
        with tm.measure("render request"):
            response = requests.post(f"{self.endpoint_url}world/render", data=request_body, headers=headers, **self._kwargs)
        if response.status_code == 200:
            # Parse the multipart response
            # is_encoded_in_gzip = response.headers["Content-Encoding"] == 'gzip'
            decoder = MultipartDecoder(response.content, response.headers['Content-Type'])

            num_params = len(camera_parameters.cameraParameters)
            images = []
            i = 0
            for part in decoder.parts:
                i += 1
                # image_row_col = int(math.ceil(math.sqrt(num_params)))
                part_body = part.content
                image_row_col = 6
                image_buffer = np.frombuffer(part_body, np.uint8)
                image_buffer = image_buffer.reshape((self._texture_size * image_row_col, self._texture_size * image_row_col, 3))
                image_buffer = np.flip(image_buffer, axis=0)

                i = 0
                for y in range(0, image_row_col):
                    for x in range(0, image_row_col):
                        num_params -= 1
                        if num_params < 0:
                            break
                        images.append(image_buffer[y * self._texture_size:(y + 1) * self._texture_size, x * self._texture_size:(x + 1) * self._texture_size])
                        i += 1
            return images
        else:
            raise RuntimeError(f'Agent Server Error: {response.status_code}')

    def request_calculate_world_bounding_box(self) -> CalculateWorldBoundingBoxResponse:
        """
        現在レンダリングサーバーが読み込んでいるすべてのUnity Sceneのすべてのコライダーを含むバウンディングボックスを計算する
        Calculate the bounding box containing all colliders of all Unity Scenes currently loaded by the rendering server
        Returns:

        """
        response = requests.get(f"{self.endpoint_url}world/bbox", **self._kwargs)
        if response.status_code == 200:
            world_bbox = self._decode_response_body(response, CalculateWorldBoundingBoxResponse)

            # 過去の実験と結果を合わせるためにBoundingBoxの各値がstrで送られている場合、それをfloatに変換する
            def convert_str_to_float(item):
                for k, v in item.__dict__.items():
                    setattr(item, k, float(getattr(item, k)))

            convert_str_to_float(world_bbox.bbox.min)
            convert_str_to_float(world_bbox.bbox.max)
            return world_bbox
        else:
            raise RuntimeError(f'Agent Server Error: {response.status_code}')

    def request_update_nodes(self, request: UpdateNodesRequest):
        """
        レンダリングサーバーに対して、表示ノードの更新をリクエストする
        これは純粋に描画用のノードのみを更新するためのAPIなので、探索ロジックの状態とは独立している
        探索ロジックのみ実装したい場合、サーバー側でこのAPIを実装する必要はない
        Request the rendering server to update the display nodes
        This is an API that updates only the nodes for drawing, so it is independent of the state of the search logic
        If you only want to implement the search logic, you don't need to implement this API on the server side
        Args:
            request:

        Returns:

        """
        headers = {'Content-Type': 'application/json'}

        request_body = self._encode_request_body(request)
        response = requests.post(f"{self.endpoint_url}world/node", data=request_body, headers=headers, **self._kwargs)
        _assert_response(response)

    def request_reset_node(self):
        """
        レンダリングサーバーに対して、表示ノードの全削除をリクエストする
        Request the rendering server to delete all display nodes
        Returns:

        """
        headers = {'Content-Type': 'application/json'}

        response = requests.post(f"{self.endpoint_url}world/node/reset", headers=headers, **self._kwargs)
        _assert_response(response)

    def update_config(self, config: UpdateConfigRequest):
        """
        レンダリングサーバーに対して、レンダラーの設定を更新する
        実質的にテクスチャサイズを変更するためのAPIだが、これは教師作成時にのみ使用する
        Request the rendering server to update the renderer settings
        This is an API for changing the texture size in practice, but it is only used when creating training data
        Args:
            config:

        Returns:

        """
        self._texture_size = config.rendererConfig.textureSize
        headers = {'Content-Type': 'application/json'}
        request_body = self._encode_request_body(config)
        response = requests.post(f"{self.endpoint_url}config", data=request_body, headers=headers, **self._kwargs)
        _assert_response(response)

    def get_server_info(self, timeout: (float, float) = (5.0, 5.0)) -> GetServerInfoResponse:
        """
        レンダリングサーバーの情報を取得する
        サーバーのバージョンと、プラットフォームの情報が含まれる
        APIのバージョンチェックとワールド読み込み時のプラットフォーム判定に使用する
        Get information about the rendering server
        Contains the server version and platform information
        Used for API version checking and platform determination when loading the world
        Args:
            timeout:

        Returns:

        """
        response = requests.get(f"{self.endpoint_url}info", timeout=timeout)
        _assert_response(response)
        return self._decode_response_body(response, GetServerInfoResponse)

    def compute_fake_photo_positions(self, num_positions: int) -> PostComputeFakePhotoPositionsResponse:
        """
        レンダリングサーバーに対して、フェイク写真の位置計算をリクエストする
        これは教師データ作成時にのみ使用する
        Request the rendering server to calculate the position of negative example photos
        This is only used when creating training data
        Args:
            num_positions:

        Returns:

        """
        response = requests.post(f"{self.endpoint_url}world/fakePhotoPositions?num={num_positions}", **self._kwargs)
        _assert_response(response)
        return self._decode_response_body(response, PostComputeFakePhotoPositionsResponse)

    def _encode_request_body(self, request) -> str:
        """
        pythonオブジェクトをJSON文字列に変換する
        pydanticに置き換えたい。いつかは。
        Convert a python object to a JSON string
        I want to replace it with pydantic. Someday.
        Args:
            request:

        Returns:

        """
        return self._json_encoder.encode(request)

    @staticmethod
    def _decode_response_body(response, cls: Type[T]) -> T:
        """
        JSON文字列をpythonオブジェクトに変換する
        Convert a JSON string to a python object
        Args:
            response:
            cls:

        Returns:

        """
        response_body = response.content.decode("utf_8_sig")
        return json.loads(response_body, object_hook=decode_as_simple_namespace)
