using System;
using UnityEngine;

public readonly struct Result<T>
{
    public StatusCode Code { get; }
    public T Value { get; }
    public string Message { get; }

    public bool IsOk => Code == StatusCode.SUCCESS;
    public bool IsError => !IsOk;

    private Result(StatusCode code, T value, string message)
    {
        Code = code;
        Value = value;
        Message = message;
    }

    static void LogError(StatusCode code, string message)
    {
        string safeMessage = string.IsNullOrEmpty(message) ? "No details provided." : message;
        Debug.LogError($"[{code} ({(int)code})] {safeMessage}");
    }

    public static Result<T> Ok(T value)
    {
        return new Result<T>(StatusCode.SUCCESS, value, null);
    }

    public static Result<T> Err(StatusCode code, string message = null)
    {
        if (code == StatusCode.SUCCESS)
        {
            throw new ArgumentException("Err cannot be created with SUCCESS code");
        }

        if (code == StatusCode.ENTRY_NOT_FOUND)
        {
            string safeMessage = string.IsNullOrEmpty(message) ? "No details provided." : message;
            Debug.LogWarning($"{code} {safeMessage}");
        }
        else
        {
            LogError(code, message);
        }
        return new Result<T>(code, default, message);
    }

    public static Result<T> Err(StatusCode code, T value, string message = null)
    {
        LogError(code, message);
        return new Result<T>(code, value, message);
    }

    public static Result<T> Err<TOther>(Result<TOther> errorResult)
    {
        if (errorResult.IsOk)
        {
            throw new ArgumentException("Error cannot be created with Ok code");
        }
        // Do not log again here to avoid duplicate logs when propagating
        return new Result<T>(errorResult.Code, default, errorResult.Message);
    }
    public void Deconstruct(out StatusCode code, out T value)
    {
        code = Code;
        value = Value;
    }
}


