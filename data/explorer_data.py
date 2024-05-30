from dataclasses import dataclass, field
from typing import Optional


@dataclass
class ScoringNetConfig:
    _argument_group_name = "Scoring Net Parameters"
    # Scoring Net parameters
    scoring_net_checkpoint: str = field(default="scoring_net.pth.tar", metadata={"help": "checkpoint for scoring net"})
    scoring_net_device: str = field(default="cuda", metadata={"help": "device for scoring net (cpu or cuda)"})


@dataclass
class LeafGridSearchConfig:
    _argument_group_name = "Leaf Grid Search Parameters"
    lower_size_bound: float = field(default=2.5, metadata={"help": "lower size bound in meters, used to prune nodes with small size"})
    score_threshold: float = field(default=0.3, metadata={"help": "lower score threshold"})


@dataclass
class HOOConfig:
    _argument_group_name = "Hierarchical Optimistic Optimization (HOO) Parameters"
    # Hyperparameters
    # see section 4.1 HOO Strategy for details

    # Metagrapher hyperparameters
    num_updates: int = field(default=300, metadata={"help": "maximum number of nodes to be explored"})
    num_local_dir: int = field(default=21, metadata={"help": "number of camera directions to be sampled each nodes"})

    # Hierarchical Optimistic Optimization (HOO) hyperparameters
    c: Optional[float] = field(default=0.2, metadata={"help": "HOO hyperparameter c(exploration term), must be greater than 0."})
    v1: Optional[float] = field(default=0.5, metadata={"help": "HOO hyperparameter v1(regularization term), must be greater than 0."})
    rho: Optional[float] = field(default=0.5, metadata={"help": "HOO hyperparameter rho(regularization term), must be greater than 0."})
    seed: Optional[int] = field(default=42, metadata={"help": "random seed for HOO"})
    policy_name: Optional[str] = field(default="size", metadata={"help": "policy name (determines strategy to split the space)"})
    value_strategy: Optional[str] = field(default="max", metadata={"help": "how to compute value for HOO (max or mean)"})

    log_root: str = field(default="./output/exploration_log", metadata={"help": "root directory for logs"})


@dataclass
class RenderAPIConfig:
    _argument_group_name = "Render API Parameters"
    api_host: Optional[str] = field(default=None, metadata={"help": "host for render server"})
    api_port: int = field(default=8080, metadata={"help": "port for render server"})
