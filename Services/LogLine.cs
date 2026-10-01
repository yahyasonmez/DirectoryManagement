namespace DirectoryManagement.Services;

public readonly record struct LogLine(string Key, params object?[] Args);
