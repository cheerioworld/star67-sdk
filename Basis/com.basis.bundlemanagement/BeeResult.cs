public enum BeeFailureKind { Unknown, Network, InvalidData, Unsupported }

public readonly struct BeeResult<T>
{
    public bool IsSuccess { get; }
    public T Value { get; }
    public string Error { get; }
    public long ResponseCode { get; }
    public BeeFailureKind FailureKind { get; }
    private BeeResult(bool ok, T value, string error, long code, BeeFailureKind kind = BeeFailureKind.Unknown)
    {
        IsSuccess = ok; Value = value; Error = error; ResponseCode = code;
        FailureKind = kind;
    }
    public static BeeResult<T> Ok(T value) => new(true, value, null, -1);
    public static BeeResult<T> Fail(string error, long responseCode = -1, BeeFailureKind kind = BeeFailureKind.Unknown) => new(false, default, error, responseCode, kind);
    public override string ToString() => IsSuccess ? $"OK: {Value}" : $"FAIL[{ResponseCode.ToString() ?? "-"}]: {Error}";
}
