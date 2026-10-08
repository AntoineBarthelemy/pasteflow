// Windows tests show real windows and use the accessibility API of the desktop: run in parallel, they compete for it
// and readings time out. They run one after the other.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
