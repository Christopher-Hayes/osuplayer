using System;
using System.Globalization;
using Material.Icons;
using NUnit.Framework;
using OsuPlayer.Data.OsuPlayer.Enums;
using OsuPlayer.Extensions.ValueConverters;

namespace OsuPlayer.Tests.ValueConverterTests;

public class RepeatConverterTests
{
    private readonly Type _expectedInput = typeof(RepeatMode);
    private readonly Type _expectedOutput = typeof(MaterialIconKind);
    private RepeatConverter _repeatConverter;

    [SetUp]
    public void Setup()
    {
        _repeatConverter = new RepeatConverter();
    }

    [TestCase(10)]
    [TestCase("test")]
    public void TestWrongInputHandled(object input)
    {
        Assert.That(input, Is.Not.InstanceOf(_expectedInput));
        Assert.DoesNotThrow(() => _repeatConverter.Convert(input, _expectedOutput, null, CultureInfo.InvariantCulture));
    }

    [Test]
    public void TestNullInputHandled()
    {
        Assert.DoesNotThrow(() => _repeatConverter.Convert(null, _expectedOutput, null, CultureInfo.InvariantCulture));
    }

    [TestCase(RepeatMode.NoRepeat)]
    [TestCase(RepeatMode.RepeatAll)]
    public void TestCorrectUsage(RepeatMode test)
    {
        var output = _repeatConverter.Convert(test, _expectedOutput, null, CultureInfo.InvariantCulture);
        Assert.That(output, Is.InstanceOf(_expectedOutput));
    }

    [TestCase(RepeatMode.NoRepeat, false)]
    [TestCase(RepeatMode.RepeatAll, false)]
    public void TestCorrectBoolUsage(RepeatMode mode, bool expected)
    {
        // The converter no longer returns bool; it only maps to MaterialIconKind.
        // Verify it returns null (not bool) for non-icon target types.
        var type = typeof(bool);
        var output = _repeatConverter.Convert(mode, type, null, CultureInfo.InvariantCulture);
        Assert.That(output, Is.Null);
    }

    [Test]
    public void TestOutputOnIncorrectInput()
    {
        var output = _repeatConverter.Convert(10, _expectedOutput, null, CultureInfo.InvariantCulture);
        Assert.That(output, Is.InstanceOf(_expectedOutput));
        Assert.That(output, Is.EqualTo(MaterialIconKind.QuestionMark));
    }
}