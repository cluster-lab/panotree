import argparse

from timm.utils import ParseKwargs


def add_scoring_net_params(parser: argparse.ArgumentParser):
    pgroup = parser.add_argument_group('Scoring Net Parameters')
    pgroup.add_argument('--model', '-m', metavar='NAME', default='vit_base_patch16_224.augreg2_in21k_ft_in1k',
                        help='model architecture (default: dpn92)')
    pgroup.add_argument('--in-chans', type=int, default=None, metavar='N',
                        help='Image input channels (default: None => 3)')
    pgroup.add_argument('--input-size', default=None, nargs=3, type=int,
                        metavar='N N N',
                        help='Input all image dimensions (d h w, e.g. --input-size 3 224 224), uses model default if empty')
    pgroup.add_argument('--num-classes', type=int, default=2,
                        help='Number classes in dataset')
    pgroup.add_argument('--class-map', default='', type=str, metavar='FILENAME',
                        help='path to class to idx mapping file (default: "")')
    pgroup.add_argument('--gp', default=None, type=str, metavar='POOL',
                        help='Global pool type, one of (fast, avg, max, avgmax, avgmaxc). Model default if None.')
    pgroup.add_argument('--log-freq', default=10, type=int,
                        metavar='N', help='batch logging frequency (default: 10)')
    pgroup.add_argument('--checkpoint', default='./model/mlphoto2023_v0_model_best.pth.tar', type=str, metavar='PATH',
                        help='path to latest checkpoint (default: none)')
    pgroup.add_argument('--pretrained', dest='pretrained', action='store_true',
                        help='use pre-trained model')
    pgroup.add_argument('--num-gpu', type=int, default=1,
                        help='Number of GPUS to use')
    pgroup.add_argument('--test-pool', dest='test_pool', action='store_true',
                        help='enable test time pool')
    pgroup.add_argument('--no-prefetcher', action='store_true', default=False,
                        help='disable fast prefetcher')
    pgroup.add_argument('--pin-mem', action='store_true', default=False,
                        help='Pin CPU memory in DataLoader for more efficient (sometimes) transfer to GPU.')
    pgroup.add_argument('--channels-last', action='store_true', default=False,
                        help='Use channels_last memory layout')
    pgroup.add_argument('--device', default='cuda', type=str,
                        help="Device (accelerator) to use.")
    pgroup.add_argument('--amp', action='store_true', default=False,
                        help='use NVIDIA Apex AMP or Native AMP for mixed precision training')
    pgroup.add_argument('--amp-dtype', default='float16', type=str,
                        help='lower precision AMP dtype (default: float16)')
    pgroup.add_argument('--amp-impl', default='native', type=str,
                        help='AMP impl to use, "native" or "apex" (default: native)')
    pgroup.add_argument('--tf-preprocessing', action='store_true', default=False,
                        help='Use Tensorflow preprocessing pipeline (require CPU TF installed')
    pgroup.add_argument('--use-ema', dest='use_ema', action='store_true',
                        help='use ema version of weights if present')
    pgroup.add_argument('--fuser', default='', type=str,
                        help="Select jit fuser. One of ('', 'te', 'old', 'nvfuser')")
    pgroup.add_argument('--fast-norm', default=False, action='store_true',
                        help='enable experimental fast-norm')
    pgroup.add_argument('--model-kwargs', nargs='*', default={}, action=ParseKwargs)

    scripting_group = parser.add_mutually_exclusive_group()
    scripting_group.add_argument('--torchscript', default=False, action='store_true',
                                 help='torch.jit.script the full model')
    scripting_group.add_argument('--torchcompile', nargs='?', type=str, default=None, const='inductor',
                                 help="Enable compilation w/ specified backend (default: inductor).")
    scripting_group.add_argument('--aot-autograd', default=False, action='store_true',
                                 help="Enable AOT Autograd support.")
