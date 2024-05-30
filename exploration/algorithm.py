import itertools

import numpy as np

from render_server.render_api_data import BoundingBox
from .hoo import HOO


def generate_list_dir(n):
    list_dir = []
    golden_ratio = (1 + np.sqrt(5)) / 2
    angle_increment = np.pi * 2 * golden_ratio

    if n == 1:
        list_dir.append(np.array([0.0, -1.0, 0.0]))
        return list_dir

    for i in range(n):
        y = 1 - (i / (n - 1)) * 2  # y goes from 1 to -1
        radius = np.sqrt(1 - y ** 2)  # radius at y
        theta = angle_increment * i
        x = np.cos(theta) * radius
        z = np.sin(theta) * radius
        list_dir.append(np.array([x, y, z]))

    return list_dir


from typing import List, Iterable, Tuple, Generator, Optional


def update_diff(dir_counter, num_local_dir, list_directions, num_local_pos, list_pos_diff, pos_counter, node_position,
                pos_diff_scale):
    # Add output to history
    # outputs.append(output)
    # output_one_node.append(output)

    # var output = await Execute(renderTexture);
    if dir_counter < num_local_dir:
        # get direction
        next_direction = list_directions[dir_counter]
        # dir_counter ++
        dir_counter += 1
    else:
        # initialize dir_counter
        dir_counter = 0
        next_direction = list_directions[dir_counter]
        dir_counter += 1
        # get pos:
        if pos_counter == 0:
            # only for just after loading scene
            next_position = node_position
            pos_counter += 1
        elif 0 < pos_counter and pos_counter < num_local_pos + 1:
            # get pos
            next_position = node_position + pos_diff_scale * list_pos_diff[pos_counter - 1]
            # pos_couter ++
            pos_counter += 1

    return next_position, next_direction


class HOOExplorer():
    def __init__(self, c, v1, rho, policyName, num_pos_diff, num_dir, value_storategy="mean") -> None:
        self.rollout = Rollout(num_pos_diff=num_pos_diff, num_dir=num_dir)
        self.model: Optional[HOO] = None
        self.node_pos = None
        self.c = c
        self.v1 = v1
        self.rho = rho
        self.policyName = policyName
        # self.setup_HOO(bounding_box_csv, c, v1, rho, policyName)
        self.scores = [] ### for step, not for batch_step.
        self.value_storategy = value_storategy
        self.depth = None  # depth of current node

    def setup_model(self, bbox: BoundingBox):
        self.model = HOO(minX=bbox.min.x, maxX=bbox.max.x,
                         minY=bbox.min.y, maxY=bbox.max.y,
                         minZ=bbox.min.z, maxZ=bbox.max.z,
                         c=self.c, v1=self.v1, rho=self.rho, policyName=self.policyName)
        self.node_pos, self.depth = self.model.sample_position()

    def get_value(self, scores: List[float]):
        if self.value_storategy == "max":
            return np.max(scores)
        elif self.value_storategy in ["mean", "avg", "average"]:
            return np.mean(scores)

    def get_camera_parameters(self, node_pos=None) -> Generator[Tuple[np.ndarray, np.ndarray], None, None]:
        """
        get camera parameters for rendering
        Args:
            node_pos: the center of the node
        Returns:
            tuples of (position, direction)
        """
        node_pos = node_pos if node_pos is not None else self.node_pos
        self.rollout.reset()

        def itr():
            while not self.rollout.finished:
                yield self.rollout.step(node_pos, pos_diff_scale=1)

        return itr()

    def batch_step(self, scores: List[float]):
        self._step_node(scores)

    def _step_node(self, scores: List[float]):
        self.model.backpropagation(self.get_value(scores))
        self.node_pos, self.depth = self.model.sample_position()
        self.rollout.reset()


class Rollout():
    def __init__(self, num_pos_diff=0, num_dir=6) -> None:
        self.finished = False
        self.num_pos_diff = num_pos_diff
        self.num_dir = num_dir
        assert self.num_dir >= 1
        assert self.num_pos_diff >= 0
        self.num = None
        self.list_directions = None
        self.list_pos_diff = None
        self.setup()
        self.counter = 0

    def setup(self):
        self.list_directions = generate_list_dir(self.num_dir)
        self.list_pos_diff = [np.array([0, 0, 0])] + generate_list_dir(self.num_pos_diff)
        self.num = len(self.list_directions) * len(self.list_pos_diff)

    def step(self, node_pos, pos_diff_scale):
        assert (self.counter < self.num)
        next_direction = self.list_directions[self.counter % self.num_dir]
        next_position = node_pos + pos_diff_scale * self.list_pos_diff[self.counter // self.num_dir]
        self.counter += 1
        self.finished = (self.counter == self.num)
        return next_position, next_direction

    def reset(self):
        self.counter = 0
        self.finished = False
