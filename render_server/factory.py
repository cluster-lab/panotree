import logging
from contextlib import suppress
from functools import partial

import torch
from torchvision import transforms as transforms

from data.explorer_data import HOOConfig, RenderAPIConfig
from exploration.algorithm import HOOExplorer
from render_server.logger import NodeLogger, NullLogger
from render_server.render_api_client import RenderAPIClient
from render_server.render_api_client_params import parse_api_client_params
from render_server.scoring_net import ScoringNet
from render_server.world_explorer_runner import WorldExplorerRunner
from render_server.wsl_utils import is_running_in_wsl, get_windows_host_ip
from timm import create_model
from timm.models import load_checkpoint

has_apex = False
has_native_amp = False
try:
    if getattr(torch.cuda.amp, 'autocast') is not None:
        has_native_amp = True
except AttributeError:
    pass

try:
    from functorch.compile import memory_efficient_fusion

    has_functorch = True
except ImportError as e:
    has_functorch = False

has_compile = hasattr(torch, 'compile')

_logger = logging.getLogger('validate')


def create_scoring_net(args) -> ScoringNet:
    # prepare
    # might as well try to validate something
    args.pretrained = args.pretrained or not args.checkpoint
    args.prefetcher = not args.no_prefetcher

    is_using_cuda = args.device.startswith('cuda')
    device_name = args.device
    if is_using_cuda:
        if not torch.cuda.is_available():
            _logger.warning('CUDA is not available, switching to CPU.')
            device_name = 'cpu'
        else:
            torch.backends.cuda.matmul.allow_tf32 = True
            torch.backends.cudnn.benchmark = True

    device = torch.device(device_name)
    # resolve AMP arguments based on PyTorch / Apex availability
    use_amp = None
    amp_autocast = suppress
    if args.amp:
        if args.amp_impl == 'apex':
            assert has_apex, 'AMP impl specified as APEX but APEX is not installed.'
            assert args.amp_dtype == 'float16'
            use_amp = 'apex'
            _logger.info('Validating in mixed precision with NVIDIA APEX AMP.')
        else:
            assert has_native_amp, 'Please update PyTorch to a version with native AMP (or use APEX).'
            assert args.amp_dtype in ('float16', 'bfloat16')
            use_amp = 'native'
            amp_dtype = torch.bfloat16 if args.amp_dtype == 'bfloat16' else torch.float16
            amp_autocast = partial(torch.autocast, device_type=device.type, dtype=amp_dtype)
            _logger.info('Validating in mixed precision with native PyTorch AMP.')
    else:
        _logger.info('Validating in float32. AMP not enabled.')

    # create model
    in_chans = 3
    if args.in_chans is not None:
        in_chans = args.in_chans
    elif args.input_size is not None:
        in_chans = args.input_size[0]
    model = create_model(
        args.model,
        pretrained=args.pretrained,
        num_classes=args.num_classes,
        in_chans=in_chans,
        global_pool=args.gp,
        scriptable=args.torchscript,
        **args.model_kwargs,
    )
    _logger.info("The model is created.")

    model = model.to(device)
    if args.channels_last:
        model = model.to(memory_format=torch.channels_last)

    if args.torchscript:
        assert not use_amp == 'apex', 'Cannot use APEX AMP with torchscripted model'
        model = torch.jit.script(model)
    elif args.torchcompile:
        assert has_compile, 'A version of torch w/ torch.compile() is required for --compile, possibly a nightly.'
        torch._dynamo.reset()
        model = torch.compile(model, backend=args.torchcompile)
    elif args.aot_autograd:
        assert has_functorch, "functorch is needed for --aot-autograd"
        model = memory_efficient_fusion(model)

    if use_amp == 'apex':
        model = amp.initialize(model, opt_level='O1')

    if args.num_gpu > 1:
        model = torch.nn.DataParallel(model, device_ids=list(range(args.num_gpu)))

    if args.num_classes is None:
        assert hasattr(model, 'num_classes'), 'Model must have `num_classes` attr if not set on cmd line/config.'
        args.num_classes = model.num_classes

    if args.checkpoint:
        load_checkpoint(model, args.checkpoint, args.use_ema)

    param_count = sum([m.numel() for m in model.parameters()])
    _logger.info('Model %s created, param count: %d' % (args.model, param_count))

    # create tranform
    normalize = transforms.Normalize(mean=[0.311, 0.321, 0.342],
                                     std=[0.076, 0.079, 0.096])

    transform = transforms.Compose([
        # transforms.Resize(256),
        # transforms.CenterCrop(224),
        transforms.ToTensor(),
        normalize,
    ])
    return ScoringNet(model, device, transform)


def create_runner(args, node_logger: NodeLogger):
    explorer = HOOExplorer(c=args.c, v1=args.v1, rho=args.rho, policyName=args.policy_name, \
                           num_pos_diff=args.num_local_pos, num_dir=args.num_local_dir,
                           value_storategy=args.value_storategy)
    scoring_net = create_scoring_net(args)
    logger = NullLogger()
    node_logger = node_logger

    api_client: RenderAPIClient = parse_api_client_params(args)
    runner = WorldExplorerRunner(scoring_net, explorer, api_client, logger, node_logger)
    return runner, explorer


def create_hoo_explorer(hoo_conf: HOOConfig):
    return HOOExplorer(c=hoo_conf.c, v1=hoo_conf.v1, rho=hoo_conf.rho, policyName=hoo_conf.policy_name, \
                       num_pos_diff=0, num_dir=hoo_conf.num_local_dir,
                       value_storategy=hoo_conf.value_strategy)


def create_render_api_client(api_client_conf: RenderAPIConfig):
    host = api_client_conf.api_host
    if host is None:
        host = "localhost" if not is_running_in_wsl() else get_windows_host_ip()

    return RenderAPIClient(f"http://{host}:{api_client_conf.api_port}/")
