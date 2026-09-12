// Reporting dates are Pakistan business dates regardless of the workstation timezone.
(DateTime, DateTime) reportPeriod(String preset, {DateTime? nowUtc}) {
  final local = (nowUtc ?? DateTime.now().toUtc()).toUtc().add(
    const Duration(hours: 5),
  );
  final day = DateTime.utc(
    local.year,
    local.month,
    local.day,
  ).subtract(const Duration(hours: 5));
  return switch (preset) {
    'Yesterday' => (day.subtract(const Duration(days: 1)), day),
    'Today' => (day, day.add(const Duration(days: 1))),
    'This Week' => (
      day.subtract(Duration(days: local.weekday - 1)),
      day
          .subtract(Duration(days: local.weekday - 1))
          .add(const Duration(days: 7)),
    ),
    'This Month' => (
      DateTime.utc(local.year, local.month).subtract(const Duration(hours: 5)),
      DateTime.utc(
        local.year,
        local.month + 1,
      ).subtract(const Duration(hours: 5)),
    ),
    'This Year' => (
      DateTime.utc(local.year).subtract(const Duration(hours: 5)),
      DateTime.utc(local.year + 1).subtract(const Duration(hours: 5)),
    ),
    '30 Days' => (
      day.subtract(const Duration(days: 29)),
      day.add(const Duration(days: 1)),
    ),
    _ => (day, day.add(const Duration(days: 1))),
  };
}
