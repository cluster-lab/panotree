from typing import List

import numpy as np


def tile_images(images: List[np.ndarray], cols: int) -> np.ndarray:
    rows = len(images) // cols
    if len(images) % cols != 0:
        rows += 1

    # Get the shape of a single image
    image_shape = images[0].shape

    # Create an empty canvas to hold the tiled images
    canvas_height = rows * image_shape[0]
    canvas_width = cols * image_shape[1]
    canvas = np.zeros((canvas_height, canvas_width, image_shape[2]), dtype=np.uint8)

    # Tile the images onto the canvas
    for i, image in enumerate(images):
        row = i // cols
        col = i % cols
        start_row = row * image_shape[0]
        start_col = col * image_shape[1]
        canvas[start_row:start_row + image_shape[0], start_col:start_col + image_shape[1]] = image

    return canvas
