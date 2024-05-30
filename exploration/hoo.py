import random
import math
from typing import Callable, Optional, Any

import numpy as np


class Node:
    def __init__(self, parent, minX, maxX, minY, maxY, minZ, maxZ, c, v1, rho, rnd_seed: int = None):
        self.parent = parent
        self.minX = minX
        self.maxX = maxX
        self.minY = minY
        self.maxY = maxY
        self.minZ = minZ
        self.maxZ = maxZ
        self.c = c
        self.rho = rho
        self.v1 = v1
        self.depth = parent.depth + 1 if parent is not None else 0
        self.root = parent.root if parent is not None else self
        self.value = float("inf")
        self.B = float("inf")
        self.explorationCount = 0
        self.sumResults = 0
        self.bestResult = float("-inf")
        self.shortcut: Optional[Node] = None
        self.children_right: Optional[Node] = None
        self.children_left: Optional[Node] = None
        self.branch_id = None if parent is not None else 1
        self.rnd_seed: int = rnd_seed if rnd_seed is not None else parent.rnd_seed

    @property
    def id(self):
        return f"{self.depth:04}-{self.branch_id:08}"

    @property
    def bin_branch_id(self):
        """
        The Branch ID is unique to the tree.
        The Branch ID is in binary and always has a leading bit of 1.
        Branch ID 0b1 represents the root node.
        For each increase in depth, perform a left logical shift once,
        and set the terminal bit to children_left = 0, children_right = 1.
        In other words, the depth is determined by which bit the leading 1 is in,
        and the following bits represent the left and right of the binary tree.
        Returns:
            str: The Branch ID.
        """
        return bin(self.branch_id)

    def explore_child_02(self):
        if self.children_left is None:
            raise Exception("Children supposed to be already created")
        elif self.children_left.B == self.children_right.B:
            if self._touch_random(lambda: random.randint(0, 1) == 0):
                return self.children_left.sample_position()
            else:
                return self.children_right.sample_position()
        elif self.children_left.B < self.children_right.B:
            return self.children_right.sample_position()
        else:
            return self.children_left.sample_position()

    def sample_position(self):
        if self.explorationCount == 0:
            self.explorationCount += 1

            x = (self.maxX + self.minX) / 2
            y = (self.maxY + self.minY) / 2
            z = (self.maxZ + self.minZ) / 2

            self.root.shortcut = self
            return (x, y, z), self.depth
        else:
            self.explorationCount += 1
            return self.explore_child_02()

    @property
    def center(self) -> (float, float, float):
        return (self.minX + self.maxX) / 2, (self.minY + self.maxY) / 2, (self.minZ + self.maxZ) / 2

    def backpropagation_from_root(self, value, iterMax, policyName):
        lastNode = self.root.shortcut
        lastNode.value = value
        lastNode.B = value
        lastNode.bestResult = value
        lastNode.split(policyName)
        lastNode.backprop(value, iterMax)

    def policy(self, policyName):
        if policyName == "xyz":
            return self.depth % 3
        elif policyName == "size":
            size = np.array([
                abs(self.maxX - self.minX),
                abs(self.maxY - self.minY),
                abs(self.maxZ - self.minZ)
            ])
            norm = np.linalg.norm(size) + 1e-6
            size = size / norm
            distribution = self.soft_max_3(size)
            sample = self._touch_random(random.random)
            s = 0
            s += distribution[0]
            if sample <= s:
                return 0
            s += distribution[1]
            if sample <= s:
                return 1
            return 2
        else:
            raise Exception("policyName must be xyz or size")

    def soft_max_3(self, vec):
        eps = 0.000001
        vec_beta = vec + eps * np.ones(3)
        exp = np.exp(vec_beta)
        sum_exp = np.sum(exp)
        return exp / sum_exp

    def split(self, policyName):
        # The method of dividing the child bounding boxes is uniquely determined by
        # the random seed and the bounding box of the root node.
        choice = self.policy(policyName)
        left_branch_id = self.branch_id << 1 | 0
        right_branch_id = self.branch_id << 1 | 1

        if choice == 0:
            middleX = (self.minX + self.maxX) / 2
            self.children_left = Node(self, self.minX, middleX, self.minY, self.maxY, self.minZ, self.maxZ, self.c, self.v1, self.rho)
            self.children_right = Node(self, middleX, self.maxX, self.minY, self.maxY, self.minZ, self.maxZ, self.c, self.v1, self.rho)
        elif choice == 1:
            middleY = (self.minY + self.maxY) / 2
            self.children_left = Node(self, self.minX, self.maxX, self.minY, middleY, self.minZ, self.maxZ, self.c, self.v1, self.rho)
            self.children_right = Node(self, self.minX, self.maxX, middleY, self.maxY, self.minZ, self.maxZ, self.c, self.v1, self.rho)
        elif choice == 2:
            middleZ = (self.minZ + self.maxZ) / 2
            self.children_left = Node(self, self.minX, self.maxX, self.minY, self.maxY, self.minZ, middleZ, self.c, self.v1, self.rho)
            self.children_right = Node(self, self.minX, self.maxX, self.minY, self.maxY, middleZ, self.maxZ, self.c, self.v1, self.rho)
        else:
            raise Exception("choice must be in 0, 1, 2")
        self.children_left.branch_id = left_branch_id
        self.children_right.branch_id = right_branch_id

    def backprop(self, value, step):
        if self.parent is not None:
            self.sumResults += value
            mean = self.sumResults / self.explorationCount
            regularisationTerm = self.v1 * (self.rho ** self.depth)
            explorationTerm = self.c * math.sqrt((2 * math.log(step)) / self.explorationCount)
            self.bestResult = max(value, self.bestResult)
            U = mean + explorationTerm + regularisationTerm
            self.B = min(U, max(self.children_left.B, self.children_right.B))
            self.parent.backprop(value, step)
        else:
            self.sumResults += value
            self.bestResult = max(value, self.bestResult)
            self.lastValue = value

    def _touch_random(self, visitor: Callable[[], Any]):
        """
        Save the current random state, set a new random seed and call the visitor.
        Then restore the random state.
        Note:
            do not call a same random method more than once in the same node, as the same value will be returned.
            This is because the random seed is set to the node id + rnd_seed.
        """
        state = random.getstate()
        random.seed(self.branch_id + self.rnd_seed)
        ret = visitor()
        random.setstate(state)
        return ret


class HOO:
    def __init__(self, minX, maxX, minY, maxY, minZ, maxZ, c, v1, rho, policyName, rnd_seed: int = 42):
        assert minX <= maxX
        assert minY <= maxY
        assert minZ <= maxZ
        self.minX = minX
        self.maxY = maxY
        self.maxX = maxX
        self.minY = minY
        self.minZ = minZ
        self.maxZ = maxZ
        self.c = c
        self.v1 = v1
        self.rho = rho
        # self.rand = rand
        self.policyName = policyName
        self.count = 0
        self.root: Optional[Node] = None
        self.rnd_seed = rnd_seed
        self.start()

    def start(self):
        self.root = Node(None, minX=self.minX, maxX=self.maxX, minY=self.minY, maxY=self.maxY, minZ=self.minZ, maxZ=self.maxZ, \
                         c=self.c, v1=self.v1, rho=self.rho, rnd_seed=self.rnd_seed)
        self.root.bestResult = float("-inf")
        self.count += 1

    def sample_position(self):
        self.count += 1
        return self.root.sample_position()

    def backpropagation(self, value):
        self.root.backpropagation_from_root(value, self.count, self.policyName)

    def traverse_tree(self, visitor: Callable[['HOO'], None], node: 'HOO' = None):
        node = self.root if node is None else node
        visitor(node)
        if node.children_left is not None:
            self.traverse_tree(visitor, node.children_left)
        if node.children_right is not None:
            self.traverse_tree(visitor, node.children_right)
