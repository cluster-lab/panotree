import os

from render_server.render_api_data import NodeViewModel


class Logger:
    def logging(self, value, depth):
        pass


class NullLogger(Logger):
    def logging(self, value, depth):
        pass


class NodeLogger:
    def log_node(self, world_id: str, node: NodeViewModel):
        pass

    def log_artifact(self):
        pass


class NullNodeLogger(NodeLogger):
    pass


class FileNodeLogger(NodeLogger):
    def __init__(self, base_path: str, session_id: str, world_id: str):
        self._base_path = base_path
        self._session_id = session_id
        self._log_file_path = f"{self._base_path}/{world_id}_{self._session_id}_nodes.jsonl"
        os.makedirs(self._base_path, exist_ok=True)

    def log_artifact(self):
        """
        log the exploration log file as a wandb artifact
        """
        # art = wandb.Artifact(name="exploration_log", type="exploration_log", metadata={"session_id": self._session_id})
        # art.add_file(self._log_file_path)e
        # wandb.log_artifact(self._log_file_path, type="nodes")

    def log_node(self, world_id: str, node: NodeViewModel):
        with open(self._log_file_path, "a") as f:
            f.write(node.model_dump_json())
            f.write("\n")
