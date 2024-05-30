from dataclasses import dataclass
from itertools import chain
from typing import List, TypeVar, Callable, Optional, Iterator, Generator, Tuple

import numpy as np
from textual.widgets import RichLog

from exploration.algorithm import Rollout
from render_server.render_api_client import RenderAPIClient
from render_server.render_api_data import RenderSceneRequest, CameraParameter, Vector3f, Bounds, PhotoScoring
from render_server.scoring_net import ScoringNet
from util import iterutils
from util.time_measure import TimeMeasure

T = TypeVar("T")


@dataclass
class GridNode:
    id: str
    position: Vector3f
    images: List[np.ndarray]
    photo_scorings: List[PhotoScoring]


class LeafGridSearcher:
    def __init__(self,
                 scoring_net: ScoringNet,
                 rollout: Rollout,
                 render_api_client: RenderAPIClient,
                 divider: int = 5):
        self.divider = divider
        self.scoring_net = scoring_net
        self.rollout = rollout
        self.render_api_client = render_api_client

    def search(self, objs: List[T], selector: Callable[[T], Bounds], on_progress: Optional[Callable[[int, T], None]] = None, num_batch: int = 144, rich_log: RichLog = None) -> Generator[Tuple[List[GridNode], T], None, None]:
        for i, obj in enumerate(objs):
            bounds = selector(obj)
            with TimeMeasure.default().measure("1 leaf"):
                cps = list(chain.from_iterable(self._camera_params_generator(bounds)))
                cps_n = [ps.cameraParameter for ps in obj.photoScorings]
                rich_log.write(f"leaf {obj.id}")
                rich_log.write(cps)
                rich_log.write(cps_n)
                images = self._render(cps)
                images_n = self._render(cps_n)
                if on_progress:
                    on_progress(i, obj)
                with TimeMeasure.default().measure("batch inference"):
                    scores = [self.scoring_net.forward(gimages) for gimages in iterutils.grouped(num_batch, iter(images))]
                    scores = list(chain.from_iterable([s.cpu().numpy() for s in scores]))
                    scores_n = [self.scoring_net.forward(gimages) for gimages in iterutils.grouped(num_batch, iter(images_n))]
                    scores_n = list(chain.from_iterable([s.cpu().numpy() for s in scores_n]))
                rich_log.write(scores)
                rich_log.write(scores_n)

                node_id = 0
                grid_nodes = []
                for g in iterutils.grouped(self.rollout.num_dir, zip(images, scores, cps)):
                    images2 = [e[0] for e in g]
                    scores2 = [e[1] for e in g]
                    cps2 = [e[2] for e in g]
                    pss = [PhotoScoring(cameraParameter=cp, score=score).filter_np() for cp, score in zip(cps2, scores2)]
                    grid_nodes.append(GridNode(f"grid {node_id}", bounds.center.filter_np(), images2, pss))

                    # for g in range(len(images2)):
                    #     images2[g] = np.array(images2[g])
                    #     img = images2[g]
                    #     score = scores2[g]
                    #     cv2.cvtColor(img, cv2.COLOR_RGB2BGR, img)
                    #     pos = ','.join([f"{e:.3f}" for e in cps2[g].position.elements])
                    #     cv2.putText(img, f"{score:.3f}, {pos}", (10, 15), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 0, 255), 2, cv2.LINE_AA)
                    #     cv2.putText(img, f"{pos}", (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 0, 255), 2, cv2.LINE_AA)
                    node_id += 1
                    # tile_img = tile_images(images2, 5)
                    # cv2.imshow(obj.id, tile_img)
                    # cv2.waitKey(0)
                yield grid_nodes, obj

    def _render(self, camera_parameters: List[CameraParameter]):
        with TimeMeasure.default().measure("render"):
            images = self.render_api_client.request_render(RenderSceneRequest(cameraParameters=camera_parameters))
        return images

    def _camera_params_generator(self, bbox):
        camera_positions = self._divide_bbox(bbox)
        for cam_pos in camera_positions:
            cp = self._get_camera_parameters(cam_pos)
            yield cp

    def _divide_bbox(self, bounds: Bounds):
        divider = self.divider - 1
        step_size = bounds.size / Vector3f(x=divider, y=divider, z=divider)

        for x in range(1, divider):
            for y in range(1, divider):
                for z in range(1, divider):
                    yield (bounds.min + Vector3f(x=x, y=y, z=z) * step_size).elements

    def _get_camera_parameters(self, node_pos):
        self.rollout.reset()
        while not self.rollout.finished:
            cp = self.rollout.step(node_pos, pos_diff_scale=1)
            yield CameraParameter(position=Vector3f.from_array(cp[0]), direction=Vector3f.from_array(cp[1])).filter_np()
