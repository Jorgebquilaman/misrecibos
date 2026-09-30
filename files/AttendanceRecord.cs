namespace ZkAttendanceService.Models;

public sealed record AttendanceRecord(string EnrollNumber, DateTime Timestamp, byte VerifyMode, byte Status);
