from types import SimpleNamespace


def decode_as_simple_namespace(d: dict):
    ns = {}
    if not isinstance(d, dict):
        return d

    for k, v in d.items():
        if isinstance(v, dict):
            ns[k] = decode_as_simple_namespace(v)
        elif isinstance(v, list):
            ns[k] = [decode_as_simple_namespace(i) for i in v]
        else:
            ns[k] = v
    return SimpleNamespace(**ns)
