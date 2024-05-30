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
    if response.status_code != 200:
        raise RuntimeError(f'Agent Server Error: {response.status_code} {response.content.decode("utf_8_sig")}')


T = TypeVar("T")


class RenderAPIClient:

    def __init__(self, endpoint_url: str):
        self.endpoint_url = endpoint_url
        self._json_encoder = CustomJsonEncoder()
        self._texture_size = 224
        self._kwargs = {
            "timeout": (5.0, 5.0)
        }
    def request_render(self, camera_parameters: RenderSceneRequest) -> List[np.ndarray]:
        """
        request the agent server to render scene and convert it into PNG, according to given camera parameters
        Args:
            camera_parameters:

        Returns:
            a list of the render results converted as PIL.Image
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
        headers = {'Content-Type': 'application/json'}

        request_body = self._encode_request_body(request)
        response = requests.post(f"{self.endpoint_url}world/node", data=request_body, headers=headers, **self._kwargs)
        _assert_response(response)

    def request_reset_node(self):
        headers = {'Content-Type': 'application/json'}

        response = requests.post(f"{self.endpoint_url}world/node/reset", headers=headers, **self._kwargs)
        _assert_response(response)

    def update_config(self, config: UpdateConfigRequest):
        self._texture_size = config.rendererConfig.textureSize
        headers = {'Content-Type': 'application/json'}
        request_body = self._encode_request_body(config)
        response = requests.post(f"{self.endpoint_url}config", data=request_body, headers=headers, **self._kwargs)
        _assert_response(response)

    def get_server_info(self, timeout: (float, float) = (5.0, 5.0)) -> GetServerInfoResponse:
        response = requests.get(f"{self.endpoint_url}info", timeout=timeout)
        _assert_response(response)
        return self._decode_response_body(response, GetServerInfoResponse)

    def compute_fake_photo_positions(self, num_positions: int) -> PostComputeFakePhotoPositionsResponse:
        response = requests.post(f"{self.endpoint_url}world/fakePhotoPositions?num={num_positions}", **self._kwargs)
        _assert_response(response)
        return self._decode_response_body(response, PostComputeFakePhotoPositionsResponse)

    def _encode_request_body(self, request) -> str:
        return self._json_encoder.encode(request)

    @staticmethod
    def _decode_response_body(response, cls: Type[T]) -> T:
        response_body = response.content.decode("utf_8_sig")
        return json.loads(response_body, object_hook=decode_as_simple_namespace)
