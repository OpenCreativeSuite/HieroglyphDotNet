using Xunit;

// The editor, the settings file and the host modifier snapshot are process-wide.
// Serial execution keeps component tests deterministic.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
