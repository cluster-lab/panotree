import os
import threading
from typing import List, Optional, Callable

from textual.app import App, ComposeResult
from textual.containers import Container
from textual.reactive import var
from textual.widgets import DirectoryTree, Footer, Header, RichLog, Label, Static, ProgressBar

from render_server.leaf_grid_searcher import LeafGridSearcher
from render_server.render_api_client import RenderAPIClient
from render_server.render_api_data import NodeViewModel, UpdateNodesRequest, PhotoScoring, CustomJsonEncoder, LeafGridNode


class PanoTreeExplorerApp(App):
    CSS_PATH = "panotree_explorer_tui.tcss"
    DEFAULT_BINDINGS = [
        ("q", "quit", "Quit"),
        ("e", "explore", "Explore"),
        # ("g", "grid_search", "Grid Search"),
    ]

    BINDINGS = DEFAULT_BINDINGS

    show_tree = var(True)
    show_progress = var(False)
    gui_enabled = var(True)

    def __init__(self,
                 base_path: str,
                 api_client: RenderAPIClient,
                 leaf_grid_searcher: LeafGridSearcher,
                 explore_action: Callable[[ProgressBar, Label], None],
                 score_threshold: float,
                 lower_size_bound: float,
                 **kwargs):
        super().__init__(**kwargs)
        self._base_path = base_path
        self._api_client = api_client
        self._leaf_grid_searcher = leaf_grid_searcher
        self._score_threshold = score_threshold
        self._explore_action = explore_action
        self._lower_size_bound = lower_size_bound  # in meters
        self._root_node: Optional[NodeViewModel] = None
        self._log_file_path: Optional[str] = None

    def watch_show_tree(self, show_tree: bool) -> None:
        """Called when show_tree is modified."""
        self.set_class(show_tree, "-show-tree")

    def watch_gui_enabled(self, gui_enabled: bool) -> None:
        dic_tree = self.query_one(DirectoryTree)
        dic_tree.disabled = not gui_enabled

    def watch_show_progress(self, show_progress: bool) -> None:
        progress_bar = self.query_one(ProgressBar)
        progress_bar.visible = show_progress

    def compose(self) -> ComposeResult:
        """Compose our UI."""
        path = self._base_path
        yield Header()
        with Container():
            with Container(id="left-pane"):
                yield Static("Exploration log selection", classes="header")
                yield DirectoryTree(path, id="tree-view")
                yield ProgressBar(id="progress-bar", show_percentage=True)
            with Container(id="right-pane"):
                yield Static("Log", classes="header")
                yield RichLog(id="log")
        yield Label(id="status-label", renderable="Press E to explore.")
        yield Footer()

    def on_mount(self) -> None:
        self.query_one(DirectoryTree).focus()

    def on_directory_tree_file_selected(
            self, event: DirectoryTree.FileSelected
    ) -> None:
        """Called when the user click a file in the directory tree."""
        event.stop()
        log_file_path = event.path
        self._log_file_path = log_file_path

        if not log_file_path.is_file() or not str(log_file_path).endswith(".jsonl"):
            return
        self._load_log(str(log_file_path))

    def _load_log(self, log_file_path: str):
        api_client = self._api_client
        self.gui_enabled = False

        api_client.request_reset_node()

        t1 = threading.Thread(target=self.load_tree_from_log, args=(log_file_path,))
        t1.start()

    def load_tree_from_log(self, log_file_path: str):
        rich_log = self.query_one(RichLog)
        progress = self.query_one(ProgressBar)
        status_label = self.query_one("#status-label", Label)
        self.show_progress = True
        with open(log_file_path) as f:
            def generator():
                line_counter = 0
                while True:
                    if line_counter % 50 == 0:
                        status_label.update(f"loading tree... {line_counter:08} nodes.")
                    line = f.readline()
                    if not line:
                        break
                    yield NodeViewModel.model_validate_json(line)
                    line_counter += 1
                    progress.advance(1)

            status_label.update(f"loading tree {log_file_path}...")
            rich_log.write(f"loading tree {log_file_path}...")
            self._root_node = NodeViewModel.create_tree(generator())

            progress.progress = 0
            status_label.update(f"pruning tree {log_file_path}...")
            rich_log.write(f"pruning tree {log_file_path}...")
            self._prune_node_leaf(self._root_node)
            status_label.update(f"uploading nodes...")
            self._upload_node(self._root_node, True)

        status_label.update(f"Ready.")
        rich_log.write(f"Done.")
        self.gui_enabled = True
        self.show_progress = False

    def _upload_node(self, root_node: NodeViewModel, recursive: bool):
        def upload_to_server(node: NodeViewModel):
            t_node = node.copy_without_children()
            self._api_client.request_update_nodes(UpdateNodesRequest(nodes=[t_node]))
            return True

        if recursive:
            root_node.traverse(upload_to_server)
        else:
            upload_to_server(root_node)

    def _prune_node_leaf(self, root: NodeViewModel):
        rich_log = self.query_one(RichLog)

        def traverse_and_prune(node: NodeViewModel):
            children = node.children
            reached_lower_bound = False
            for child in children:
                # if any of the children has a size that is smaller than the lower bound, we stop traversing
                reached_lower_bound |= any([e < self._lower_size_bound for e in child.size.elements])

            if not reached_lower_bound:
                for child in children:
                    traverse_and_prune(child)
                return

            def collect_photo_scorings(node_1: NodeViewModel) -> List[PhotoScoring]:
                ret = []
                if node_1.photoScorings is not None:
                    ret.extend(node.photoScorings)
                for child_1 in node_1.children:
                    ret.extend(collect_photo_scorings(child_1))
                return ret

            node.photoScorings = collect_photo_scorings(node)
            node.child_left = None
            node.child_right = None
            rich_log.write(f"lower bound reached: {node.id}, size: {child.size.elements}, photo count: {len(node.photoScorings)}")

        traverse_and_prune(root)

    def _explore(self):
        try:
            rich_log = self.query_one(RichLog)
            status_label = self.query_one("#status-label", Label)

            rich_log.write("Exploring...")
            status_label.update(f"Exploring...")

            self.gui_enabled = False
            self.show_progress = True
            self._explore_action(self.query_one(ProgressBar), status_label)
            rich_log.write("Done.")
            status_label.update(f"Ready.")
        finally:
            self.gui_enabled = True
            self.show_progress = False

    def _warn_disabled_action(self):
        rich_log = self.query_one(RichLog)
        rich_log.write("Another action is in progress. Please wait.")

    def action_explore(self):
        if not self.gui_enabled:
            self._warn_disabled_action()
            return

        t1 = threading.Thread(target=self._explore)
        t1.start()

    def action_grid_search(self):
        if not self.gui_enabled:
            self._warn_disabled_action()
            return

        rich_log = self.query_one(RichLog)
        status_label = self.query_one("#status-label", Label)
        self.gui_enabled = False

        def _grid_search():
            if self._root_node is None:
                rich_log.write("Load a log first.")
                self.gui_enabled = True
                return

            rich_log.write("Performing grid search...")
            status_label.update(f"Performing grid search...")
            lgs = self._leaf_grid_searcher
            node_list: List[NodeViewModel] = []

            def collect_nodes(node: NodeViewModel):
                if node.score >= self._score_threshold:
                    node_list.append(node)
                return True

            self._root_node.traverse(collect_nodes)

            def on_progress(i, node):
                status_label.update(f"Grid search: {node.id} {i}/{len(node_list)}")

            out_file_path = str(self._log_file_path).replace(".jsonl", "_grid_search.jsonl")
            if os.path.exists(out_file_path):
                os.remove(out_file_path)
            for grid_nodes, node in lgs.search(node_list, lambda n: n.bounds, on_progress, rich_log=rich_log):
                status_label.update(f"Evaluating node {node.id}...")
                rich_log.write(f"node: {node.id}, num grid nodes: {len(grid_nodes)}")

                leaf_grids = []
                for gn in grid_nodes:
                    gid = f"{node.id} {gn.id}"
                    leaf_grid = LeafGridNode(gridId=gid, nodeId=node.id, position=gn.position, photoScorings=gn.photo_scorings)
                    leaf_grids.append(leaf_grid)
                node.leafGridNodes = leaf_grids
                self._upload_node(node, False)
                with open(out_file_path, "a") as f:
                    f.write(CustomJsonEncoder().encode(node) + "\n")
            status_label.update(f"Grid search done.")
            self.gui_enabled = True

        t1 = threading.Thread(target=_grid_search)
        t1.start()
