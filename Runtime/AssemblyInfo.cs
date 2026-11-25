// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.CompilerServices;

// the package's editor tests see what is internal here
// (SfxVariationSet.PretendNativeMissing: a set on a platform without the native library)
[assembly: InternalsVisibleTo("CatSplat.SfxForge.Editor.Tests")]
