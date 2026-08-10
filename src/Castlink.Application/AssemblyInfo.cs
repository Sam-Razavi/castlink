using System.Runtime.CompilerServices;

// Lets Castlink.Application.Tests assert against internal watermark-key constants without making
// them part of the public API.
[assembly: InternalsVisibleTo("Castlink.Application.Tests")]
