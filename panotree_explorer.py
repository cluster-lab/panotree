import random
import time

import numpy as np
from textual.widgets import ProgressBar, Label

from data.explorer_data import HOOConfig, RenderAPIConfig, LeafGridSearchConfig
from exploration.algorithm import Rollout
from render_server import factory
from render_server.leaf_grid_searcher import LeafGridSearcher
from render_server.logger import FileNodeLogger, NullNodeLogger, NullLogger
from render_server.scoring_net_params import add_scoring_net_params
from render_server.world_explorer_runner import WorldExplorerRunner
from tools.panotree_explorer_tui import PanoTreeExplorerApp
from util.hf_argparser import HfArgumentParser
from util.time_measure import TimeMeasure


def main():
    parser = HfArgumentParser((HOOConfig, RenderAPIConfig, LeafGridSearchConfig))

    add_scoring_net_params(parser)

    hoo_conf: HOOConfig
    api_client_conf: RenderAPIConfig
    grid_conf: LeafGridSearchConfig

    hoo_conf, api_client_conf, grid_conf, args = parser.parse_args_into_dataclasses(return_remaining_strings=False)

    random.seed(hoo_conf.seed)
    np.random.seed(hoo_conf.seed)

    logger = NullLogger()
    node_logger = NullNodeLogger()
    session_id = f"{time.time()}"
    if hoo_conf.log_root:
        node_logger = FileNodeLogger(hoo_conf.log_root, session_id, "explore")
    scoring_net = factory.create_scoring_net(args)
    explorer = factory.create_hoo_explorer(hoo_conf)
    api_client = factory.create_render_api_client(api_client_conf)
    runner = WorldExplorerRunner(
        scoring_net=scoring_net,
        explorer=explorer,
        api_client=api_client,
        node_logger=node_logger,
        logger=logger
    )

    lgs = LeafGridSearcher(scoring_net, Rollout(0, hoo_conf.num_local_dir), api_client, 5)

    def explore_action(progress_bar: ProgressBar, status_label: Label):
        runner.reset_nodes()
        bbox = runner.calculate_bounding_box()

        tm = TimeMeasure.default()

        progress_bar.total = hoo_conf.num_updates

        for i in range(hoo_conf.num_updates):
            status_label.update(f"Exploring {i:08}/{hoo_conf.num_updates}...")
            with tm.measure("evaluate_leaf"):
                runner.evaluate_leaf(bbox)
                progress_bar.advance()

        tm.print_avg()

    PanoTreeExplorerApp(
        base_path=hoo_conf.log_root,
        leaf_grid_searcher=lgs,
        api_client=api_client,
        explore_action=explore_action,
        score_threshold=grid_conf.score_threshold,
        lower_size_bound=grid_conf.lower_size_bound
    ).run()


if __name__ == "__main__":
    main()
