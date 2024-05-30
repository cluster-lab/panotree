import argparse

from render_server.render_api_client import RenderAPIClient
from render_server.wsl_utils import is_running_in_wsl, get_windows_host_ip


def add_api_client_params(parser: argparse.ArgumentParser):
    parser.add_argument('--api_host', '-H', type=str)
    parser.add_argument('--api_port', '-P', type=int, default=8080)


def parse_api_client_params(args) -> RenderAPIClient:
    host = args.api_host
    if host is None:
        host = "localhost" if not is_running_in_wsl() else get_windows_host_ip()
    return RenderAPIClient(f"http://{host}:{args.api_port}/")
