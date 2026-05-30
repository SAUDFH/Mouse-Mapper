using MouseMapper.Models;
using MouseMapper.Services;
using System.IO;
using System.Text.Json;

namespace MouseMapper.Tests;

[TestFixture]
public class ConfigManagerTests
{
    private string _testDir = null!;
    private ConfigManager _configManager = null!;

    [SetUp]
    public void SetUp()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "MouseMapperTests", Guid.NewGuid().ToString());
        _configManager = new ConfigManager(_testDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Test]
    public void Load_FileDoesNotExist_CreatesDefaultAndReturnsIt()
    {
        // Act
        var config = _configManager.Load();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(config.Version, Is.EqualTo(1));
            Assert.That(config.Steering.Deadzone, Is.EqualTo(0.03f));
            Assert.That(config.Throttle.LowSlope, Is.EqualTo(1.5f));
            Assert.That(config.Activation.ToggleKey, Is.EqualTo("Oem3"));
        });
    }

    [Test]
    public void Load_FileExists_LoadsAndReturnsIt()
    {
        // Arrange
        var customConfig = new AppConfig
        {
            Version = 5,
            Steering = new CurveParameters
            {
                Deadzone = 0.05f,
                LowSlope = 0.8f,
                KneePoint = 0.45f,
                HighSlope = 3.0f,
                SmoothingMs = 100
            },
            Throttle = new CurveParameters
            {
                Deadzone = 0.1f,
                LowSlope = 0.5f,
                KneePoint = 0.6f,
                HighSlope = 0.7f,
                SmoothingMs = 40
            },
            Activation = new ActivationConfig
            {
                ToggleKey = "F1",
                ThrottleResetButton = "Button1"
            },
            Osd = new OsdConfig
            {
                Enabled = false,
                Scale = 2.0,
                PositionX = 100,
                PositionY = 200
            }
        };

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(customConfig, jsonOptions);
        var configDir = Path.GetDirectoryName(Path.Combine(_testDir, "config.json"))!;
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(_testDir, "config.json"), json);

        // Act
        var loaded = _configManager.Load();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(loaded.Version, Is.EqualTo(5));
            Assert.That(loaded.Steering.Deadzone, Is.EqualTo(0.05f));
            Assert.That(loaded.Steering.LowSlope, Is.EqualTo(0.8f));
            Assert.That(loaded.Steering.KneePoint, Is.EqualTo(0.45f));
            Assert.That(loaded.Steering.HighSlope, Is.EqualTo(3.0f));
            Assert.That(loaded.Steering.SmoothingMs, Is.EqualTo(100));
            Assert.That(loaded.Throttle.Deadzone, Is.EqualTo(0.1f));
            Assert.That(loaded.Throttle.LowSlope, Is.EqualTo(0.5f));
            Assert.That(loaded.Throttle.KneePoint, Is.EqualTo(0.6f));
            Assert.That(loaded.Throttle.HighSlope, Is.EqualTo(0.7f));
            Assert.That(loaded.Throttle.SmoothingMs, Is.EqualTo(40));
            Assert.That(loaded.Activation.ToggleKey, Is.EqualTo("F1"));
            Assert.That(loaded.Activation.ThrottleResetButton, Is.EqualTo("Button1"));
            Assert.That(loaded.Osd.Enabled, Is.False);
            Assert.That(loaded.Osd.Scale, Is.EqualTo(2.0));
            Assert.That(loaded.Osd.PositionX, Is.EqualTo(100));
            Assert.That(loaded.Osd.PositionY, Is.EqualTo(200));
        });
    }

    [Test]
    public void Load_FileCorrupted_ReturnsDefaultsAndDeletesCorruptedFile()
    {
        // Arrange
        var configDir = Path.GetDirectoryName(Path.Combine(_testDir, "config.json"))!;
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(_testDir, "config.json");
        File.WriteAllText(configPath, "this is not valid json {{{");

        // Act
        var config = _configManager.Load();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(config.Version, Is.EqualTo(1));
            Assert.That(config.Steering.Deadzone, Is.EqualTo(0.03f));
            Assert.That(config.Throttle.LowSlope, Is.EqualTo(1.5f));
            Assert.That(config.Activation.ToggleKey, Is.EqualTo("Oem3"));
            // Corrupted file is deleted and replaced with fresh defaults
            Assert.That(File.Exists(configPath), Is.True, "Default config file should exist after recovery");
            var savedJson = File.ReadAllText(configPath);
            Assert.That(savedJson, Does.Contain("\"version\""), "Saved file should contain valid default config");
            Assert.That(savedJson, Does.Not.Contain("not valid json"), "Corrupted content should be gone");
        });
    }

    [Test]
    public void Save_WritesFileAndCanReload()
    {
        // Arrange
        var config = new AppConfig
        {
            Version = 3,
            Steering = new CurveParameters
            {
                Deadzone = 0.07f,
                LowSlope = 0.4f,
                KneePoint = 0.5f,
                HighSlope = 2.0f,
                SmoothingMs = 60
            },
            Throttle = new CurveParameters
            {
                Deadzone = 0.02f,
                LowSlope = 1.0f,
                KneePoint = 0.4f,
                HighSlope = 0.6f,
                SmoothingMs = 25
            },
            Activation = new ActivationConfig
            {
                ToggleKey = "F2",
                ThrottleResetButton = "Button2"
            },
            Osd = new OsdConfig
            {
                Enabled = true,
                Scale = 1.5,
                PositionX = 50,
                PositionY = -20
            }
        };

        // Act
        _configManager.Save(config);

        var configPath = Path.Combine(_testDir, "config.json");
        Assert.That(File.Exists(configPath), Is.True, "Config file should exist after Save");

        var loaded = _configManager.Load();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(loaded.Version, Is.EqualTo(3));
            Assert.That(loaded.Steering.Deadzone, Is.EqualTo(0.07f));
            Assert.That(loaded.Steering.LowSlope, Is.EqualTo(0.4f));
            Assert.That(loaded.Steering.KneePoint, Is.EqualTo(0.5f));
            Assert.That(loaded.Steering.HighSlope, Is.EqualTo(2.0f));
            Assert.That(loaded.Steering.SmoothingMs, Is.EqualTo(60));
            Assert.That(loaded.Throttle.Deadzone, Is.EqualTo(0.02f));
            Assert.That(loaded.Throttle.LowSlope, Is.EqualTo(1.0f));
            Assert.That(loaded.Throttle.KneePoint, Is.EqualTo(0.4f));
            Assert.That(loaded.Throttle.HighSlope, Is.EqualTo(0.6f));
            Assert.That(loaded.Throttle.SmoothingMs, Is.EqualTo(25));
            Assert.That(loaded.Activation.ToggleKey, Is.EqualTo("F2"));
            Assert.That(loaded.Activation.ThrottleResetButton, Is.EqualTo("Button2"));
            Assert.That(loaded.Osd.Enabled, Is.True);
            Assert.That(loaded.Osd.Scale, Is.EqualTo(1.5));
            Assert.That(loaded.Osd.PositionX, Is.EqualTo(50));
            Assert.That(loaded.Osd.PositionY, Is.EqualTo(-20));
        });
    }
}
