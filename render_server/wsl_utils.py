import os
import subprocess


def get_platform_name():
    if hasattr(os, "uname"):
        return os.uname().sysname
    elif os.name == "nt":
        return "Windows"
def is_running_in_wsl():
    platform_name = get_platform_name()
    return platform_name == "Linux" and "-WSL2" in subprocess.check_output(["uname", "-a"]).decode("utf_8_sig")


def get_windows_host_ip():
    if not is_running_in_wsl():
        raise RuntimeError("This function should only be called from WSL")

    agent_ip = os.getenv("AGENT_IP")
    if agent_ip:
        return agent_ip
    agent_ip = subprocess.check_output("/mnt/c/windows/system32/ipconfig.exe | grep -a IPv4 | head -n1 | awk -F': ' '{ print $2 }' | head -n 1 | tr -d '\n' | tr -d '\r'", shell=True).decode()
    return agent_ip
