using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Xbox360Btn = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button;

namespace MouseMapper.Services;

public class ViGEmManager : IDisposable
{
    private ViGEmClient? _client;
    private IXbox360Controller? _controller;
    private Thread? _updateThread;
    private volatile bool _running;
    private volatile float _steering;   // [-1, 1]
    private volatile float _throttle;   // [0, 1]
    private volatile int _gearUpFrames;
    private volatile int _gearDownFrames;

    public bool IsConnected => _controller != null;

    public void SetSteering(float value)
    {
        _steering = Math.Clamp(value, -1f, 1f);
    }

    public void SetThrottle(float value)
    {
        _throttle = Math.Clamp(value, 0f, 1f);
    }

    public void PressGearUp() => _gearUpFrames = 2;
    public void PressGearDown() => _gearDownFrames = 2;

    public bool Connect()
    {
        try
        {
            _client = new ViGEmClient();
            _controller = _client.CreateXbox360Controller();
            _controller.Connect();
            _running = true;
            _updateThread = new Thread(UpdateLoop)
            {
                Name = "ViGEmUpdateThread",
                IsBackground = true
            };
            _updateThread.Start();
            return true;
        }
        catch (Exception)
        {
            _controller = null;
            _client?.Dispose();
            _client = null;
            return false;
        }
    }

    private void UpdateLoop()
    {
        while (_running)
        {
            if (_controller != null)
            {
                short thumbX = (short)(_steering * 32767);
                byte rightTrigger = (byte)(_throttle * 255);
                _controller.SetAxisValue(Xbox360Axis.LeftThumbX, thumbX);
                _controller.SetSliderValue(Xbox360Slider.RightTrigger, rightTrigger);

                _controller.SetButtonState(Xbox360Btn.B, _gearUpFrames > 0);
                _controller.SetButtonState(Xbox360Btn.X, _gearDownFrames > 0);
                if (_gearUpFrames > 0) _gearUpFrames--;
                if (_gearDownFrames > 0) _gearDownFrames--;
            }
            Thread.Sleep(16); // ~60Hz
        }
    }

    public void Disconnect()
    {
        _running = false;
        _updateThread?.Join(500);
        _controller?.Disconnect();
        _controller = null;
        _client?.Dispose();
        _client = null;
    }

    public void Dispose()
    {
        Disconnect();
    }
}
