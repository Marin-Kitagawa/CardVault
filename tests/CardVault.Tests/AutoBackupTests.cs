using System;
using System.IO;
using CardVault.Services;
using Xunit;

namespace CardVault.Tests;

public class AutoBackupTests
{
    [Fact]
    public void IsDue_OffCadenceNeverRuns() => Assert.False(AutoBackupService.IsDue(null, "", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));

    [Theory]
    [InlineData("daily")]
    [InlineData("weekly")]
    [InlineData("monthly")]
    public void IsDue_NeverBackedUpRunsImmediately(string cadence) =>
        Assert.True(AutoBackupService.IsDue(null, cadence, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void IsDue_DailySameDayNotDue() =>
        Assert.False(AutoBackupService.IsDue(new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero), "daily", new DateTimeOffset(2026, 9, 19, 23, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void IsDue_DailyNextDayDue() =>
        Assert.True(AutoBackupService.IsDue(new DateTimeOffset(2026, 9, 18, 3, 0, 0, TimeSpan.Zero), "daily", new DateTimeOffset(2026, 9, 19, 3, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void IsDue_WeeklyAfterSevenDaysDue() =>
        Assert.False(AutoBackupService.IsDue(new DateTimeOffset(2026, 9, 18, 0, 0, 0, TimeSpan.Zero), "weekly", new DateTimeOffset(2026, 9, 24, 23, 59, 0, TimeSpan.Zero)));

    [Fact]
    public void IsDue_WeeklyPastSevenDaysDue() =>
        Assert.True(AutoBackupService.IsDue(new DateTimeOffset(2026, 9, 18, 0, 0, 0, TimeSpan.Zero), "weekly", new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void BackupFileName_IsLexicographicallyTimestamped()
    {
        var a = AutoBackupService.BackupFileName(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var b = AutoBackupService.BackupFileName(new DateTimeOffset(2026, 1, 2, 3, 4, 6, TimeSpan.Zero));
        Assert.Equal("cardvault-auto-20260102-030405.cvault", a);
        Assert.StartsWith("cardvault-auto-", a);
        Assert.True(string.CompareOrdinal(a, b) < 0);
    }

    [Fact]
    public void Prune_KeepsNewestRetainFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cvbackup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            for (var i = 1; i <= 5; i++)
                File.WriteAllText(Path.Combine(dir, AutoBackupService.BackupFileName(new DateTimeOffset(2026, 9, i, 0, 0, 0, TimeSpan.Zero))), "x");

            var deleted = AutoBackupService.Prune(dir, 3);

            Assert.Equal(2, deleted);
            var remaining = Directory.GetFiles(dir, "cardvault-auto-*.cvault");
            Assert.Equal(3, remaining.Length);
            Assert.DoesNotContain(remaining, f => Path.GetFileName(f).StartsWith("cardvault-auto-20260901"));
            Assert.DoesNotContain(remaining, f => Path.GetFileName(f).StartsWith("cardvault-auto-20260902"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Prune_RetainOneKeepsOnlyNewest()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cvbackup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            for (var i = 1; i <= 3; i++)
                File.WriteAllText(Path.Combine(dir, AutoBackupService.BackupFileName(new DateTimeOffset(2026, 9, i, 0, 0, 0, TimeSpan.Zero))), "x");

            var deleted = AutoBackupService.Prune(dir, 1);

            Assert.Equal(2, deleted);
            var remaining = Directory.GetFiles(dir, "cardvault-auto-*.cvault");
            Assert.Single(remaining);
            Assert.Contains("20260903", remaining[0]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}