using hcontroller.Lyuma;
using VRC.OSCQuery;
using static VRC.OSCQuery.Attributes.AccessValues;

namespace Hai.HView.OSC;

public struct HOscItem
{
    public string Key;
    public bool IsReadable;
    public bool IsWritable;
    public object[] Values;
    public string OscType;
    public string Description;
    public bool IsAbsentFromQuery;
    public object WriteOnlyValueRef;
    public bool IsFlipping;
    public bool FlipStart;
    public bool IsDisabled;
    public int DifferentValueCount;
}

/// <summary>
/// Combines the state of OSC addresses through both received OSC messages and OSC query responses.
/// </summary>
public class HMessageBox
{
    private readonly Dictionary<string, HOscItem> _messages = new();

    public void ReceivedOsc(string key, object[] values)
    {
        if (_messages.ContainsKey(key))
        {
            var item = _messages[key];
            var previousValues = item.Values;
            var currentValues = RewriteValues(values);
            item.Values = currentValues;
            item.WriteOnlyValueRef = RewriteWriteOnlyValueRef(values);
            item.IsDisabled = false;
            if (previousValues.Length == item.Values.Length)
            {
                for (var index = 0; index < previousValues.Length; index++)
                {
                    var previousValue = previousValues[index];
                    var currentValue = currentValues[index];
                    if (previousValue != currentValue)
                    {
                        item.DifferentValueCount += 1;
                        break;
                    }
                }
            }
            _messages[key] = item;
        }
        else
        {
            var sb = new System.Text.StringBuilder();
            SimpleOSC.GenerateOSCTypeTagInto(sb, values);
            var oscType = sb.ToString();
            _messages[key] = new HOscItem
            {
                Key = key,
                IsWritable = false,
                IsReadable = true,
                IsAbsentFromQuery = true,
                Description = "",
                OscType = oscType,
                Values = RewriteValues(values),
                WriteOnlyValueRef = RewriteWriteOnlyValueRef(values),
                IsDisabled = false,
                DifferentValueCount = 0
            };
        }
    }

    public void ReceivedQuery(OSCQueryNode parameter)
    {
        var oscKey = parameter.FullPath;

        if (!_messages.TryGetValue(oscKey, out var oscItem))
        {
            oscItem = new HOscItem
            {
                // Only write values if it's a new item.
                // Values obtained from OSC have priority
                Values = RewriteValues(parameter.Value),
                WriteOnlyValueRef = RewriteWriteOnlyValueRef(parameter.Value)
            };
        }

        oscItem.Key = oscKey;
        oscItem.OscType = parameter.OscType ?? "";
        oscItem.IsReadable = parameter.Access is ReadWrite or ReadOnly;
        oscItem.IsWritable = parameter.Access is ReadWrite or WriteOnly;
        oscItem.Description = parameter.Description ?? "";
        // field may have been true if OSC received before OSCQuery registered it
        oscItem.IsAbsentFromQuery = false;
        oscItem.IsDisabled = false;
        _messages[oscKey] = oscItem;
    }

    private object RewriteWriteOnlyValueRef(object[] parameterValue)
    {
        return parameterValue != null && parameterValue.Length > 0 ? Reform(parameterValue[0]) : 0f;
    }

    private object[] RewriteValues(object[] parameterValue)
    {
        return parameterValue == null ? new object[0] : parameterValue.Select(Reform).ToArray();
    }

    private object Reform(object o)
    {
        if (o is double) return (float)(double)o;
        if (o is long) return (int)(long)o;
        return o;
    }

    public void Reset()
    {
        foreach (var message in _messages)
        {
            var item = message.Value;
            item.IsDisabled = true;
            _messages[message.Key] = item;
        }
    }

    public Dictionary<string, HOscItem> CopyForUi()
    {
        return new Dictionary<string, HOscItem>(_messages);
    }

    public void RewriteIfNecessary(string key, object singleValue)
    {
        if (_messages.ContainsKey(key))
        {
            var oscItem = _messages[key];
            oscItem.WriteOnlyValueRef = singleValue;
            _messages[key] = oscItem;
        }
    }

    // Tracks the change of a held down button.
    // On change, inverts the value of the key, keeping track of whether that is being held down.
    // Returns the change event that happened, and outputs the value of the state when it started.
    public FlipState SubmitFlipState(string key, bool isHeldDown, out bool initialValue)
    {
        if (_messages.TryGetValue(key, out var message))
        {
            var wasFlipping = message.IsFlipping;
            message.IsFlipping = isHeldDown;
            if (isHeldDown != wasFlipping)
            {
                if (isHeldDown)
                {
                    var startValue = message.WriteOnlyValueRef is bool ? (bool)message.WriteOnlyValueRef : false;
                    message.FlipStart = startValue;
                    initialValue = startValue;
                    _messages[key] = message;
                    return FlipState.OnPress;
                }
                else
                {
                    initialValue = message.FlipStart;
                    _messages[key] = message;
                    return FlipState.OnRelease;
                }
            }
        }

        initialValue = false;
        return FlipState.None;
    }

    public enum FlipState
    {
        None,
        OnPress,
        OnRelease
    }
}