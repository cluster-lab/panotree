import traceback
from typing import Optional

import torch

from exploration.algorithm import HOOExplorer
from render_server.logger import Logger, NodeLogger
from render_server.render_api_client import RenderAPIClient
from render_server.render_api_data import CameraParameter, Vector3f, RenderSceneRequest, BoundingBox, NodeViewModel, UpdateNodesRequest, PhotoScoring
from render_server.scoring_net import ScoringNet
from util.time_measure import TimeMeasure


class WorldExplorerRunner:
    def __init__(self,
                 scoring_net: ScoringNet,
                 explorer: HOOExplorer,
                 api_client: RenderAPIClient,
                 logger: Logger,
                 node_logger: Optional[NodeLogger]):

        self.scoring_net = scoring_net
        self.explorer = explorer
        self.api_client = api_client
        self.logger = logger
        self._node_logger = node_logger
        self._world_id = "world1"

    def calculate_bounding_box(self) -> BoundingBox:
        bbox = self.api_client.request_calculate_world_bounding_box()
        return bbox.bbox

    def reset_nodes(self):
        self.api_client.request_reset_node()

    def evaluate_leaf(self, bbox: BoundingBox):
        try:
            tm = TimeMeasure.default()

            if self.explorer.model is None:
                self.explorer.setup_model(bbox)

            with tm.measure("http request (rendering)"):
                raw_camera_parameters = list(self.explorer.get_camera_parameters())

                def map_camera_parameter(cp):
                    next_pos, next_dir = cp
                    return CameraParameter(position=Vector3f.from_array(next_pos), direction=Vector3f.from_array(next_dir))

                camera_parameters = list(map(map_camera_parameter, raw_camera_parameters))
                request_body = RenderSceneRequest(cameraParameters=camera_parameters)

                images = self.api_client.request_render(request_body)
            with torch.no_grad():
                with tm.measure("inference scoring net"):
                    scores = self.scoring_net.forward(images).cpu().numpy()
                    last_evaluated_node = self.explorer.model.root.shortcut
                    self.explorer.batch_step(scores)

                    # import numpy as np
                    # for g in range(len(images)):
                    #     images[g] = np.array(images[g])
                    #     img = images[g]
                    #     score = scores[g]
                    #     cv2.cvtColor(img, cv2.COLOR_RGB2BGR, img)
                    #     cv2.putText(img, f"{score}", (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 0, 255), 2, cv2.LINE_AA)
                    # tile_img = tile_images(images, 5)
                    # cv2.imshow(last_evaluated_node.id, tile_img)
                    # cv2.waitKey(0)

                with tm.measure("visualize"):
                    photo_scoring = [PhotoScoring(cameraParameter=cp, score=float(score)) for score, cp in zip(scores, camera_parameters)]
                    node = NodeViewModel.from_node(last_evaluated_node, photo_scoring)
                    self.api_client.request_update_nodes(UpdateNodesRequest(nodes=[node]))

                with tm.measure("logging"):
                    self.logger.logging(self.explorer.get_value(scores), self.explorer.depth)

                    if self._node_logger is None:
                        return
                    self._node_logger.log_node(self._world_id, node)


        except Exception as e:
            print("An error occured")
            print("The information of error is as following")
            traceback.print_exc()
            raise e
