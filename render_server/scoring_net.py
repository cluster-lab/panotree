from typing import List

import torch
import numpy as np
import torchvision
from PIL import Image

from util.time_measure import TimeMeasure


class ScoringNet:

    def __init__(self,
                 model: torch.nn.Module,
                 device: torch.device,
                 transform: torchvision.transforms.Compose):
        self.model = model
        self.transform = transform
        self.device = device
        self.model.eval()

    def forward(self, images: List[np.ndarray]) -> torch.Tensor:
        tm = TimeMeasure.default()
        with torch.no_grad():
            with tm.measure("image conversion"):
                images = [Image.fromarray(img) for img in images]
            with tm.measure("inference"):
                x = torch.stack([self.transform(img) for img in images], dim=0)
                x = x.to(self.device)
                x = x.reshape(len(images), *x.shape[1:])
                x = self.model(x)
                scores = torch.nn.functional.softmax(x, dim=-1)[:, 1]
                return scores
