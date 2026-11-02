# SSRS2's SkiaSharp native library

SkiaSharp's native library (`libSkiaSharp`) as [SSRS2](https://ssrs2.net) builds it: SkiaSharp 3.119.4's own sources
and build scripts, with three changes: a PDF's streams are deflated at the zlib level the document asks for, zlib's
fastest when it names none; the PDF backend writes each run of text without asking again what it knows of the run's
font; and a thread waiting for one of Skia's locks spins a few microseconds before it sleeps. It takes the place of the library in the `SkiaSharp.NativeAssets.*` 3.119.4 packages: same file
name, same API, used with the `SkiaSharp` 3.119.4 package.

On SSRS2's 300-page test report, writing the PDF took 248 ms against 315 ms with SkiaSharp's own library (warm, on an
M-series Mac), and the PDFs SSRS2's bench writes render the same pixel for pixel (macOS's PDF renderer), 2% to 5%
larger.

## What it changes

`patches/skia/0001-ssrs2.patch`, on the Skia SkiaSharp pins: the PDF documents the C API makes
(`sk_document_create_pdf_from_stream...`) deflate their streams at the level the metadata asks for, else at
`SkPDF::Metadata::CompressionLevel::LowButFast`, zlib's level 1, in place of the PDF backend's default (zlib's level 6).
SkiaSharp's bindings have no field for it, so it comes in `SKDocumentPdfMetadata.EncodingQuality`: a quality of 1000 or
more is the zlib level plus one in thousands, plus the quality itself:

| `EncodingQuality` | Streams deflated at | Images |
| --- | --- | --- |
| up to 101 (SkiaSharp's own values) | zlib's level 1, the fastest | as SkiaSharp says (101: lossless) |
| 1101 | not deflated | lossless |
| 2101 | level 1, the fastest | lossless |
| 7101 | level 6, the PDF backend's default | lossless |
| 10101 | level 9, the smallest | lossless |

(`2050` is level 1 with images as JPEG at quality 50, and so on.) SkiaSharp's own library reads such a quality as above
100, lossless, and deflates at its default level. Deflating each page's content at the default level was a large part
of writing a long document. Every stream the PDF backend deflates is concerned (page content, forms, fonts, the images
it does not write as JPEG); nothing else changes.

`patches/skia/0002-ssrs2-pdf-text.patch`: what the PDF backend wrote each run of text with, found again for every run
(`SkPDFDevice::internalDrawGlyphRun`), is found once per typeface in the document: the font's type (which asked the
platform's font whether it has a COLR table, twice a run), its units per em and glyph count, and the strike of its glyph
outlines (a descriptor made and looked up in the glyph cache at every run). A cluster's end is the next glyph's cluster
when a run's clusters never decrease (`SkClusterator`, which searched the whole run for each cluster). The PDFs are byte
for byte those without it (SSRS2's 22 bench PDFs and the 100 of its report corpus, macOS arm64). On SSRS2's 300-page test report
the PDF's writing took 105 to 118 ms against 132 to 135 without it (warm, on an M-series Mac; the font table queries
are macOS's most).

`patches/skia/0003-ssrs2-semaphore-spin.patch`: `SkMutex` is `SkSemaphore`, whose waiters slept at once. Locks held for a
moment by threads taking them in turn (a glyph strike's, which every run of text of a font takes, and the glyph cache's)
made each waiter sleep and wait for a wakeup that took longer than the lock was held, so that documents written at once
in one process with the same fonts queued on them. `SkSemaphore::wait` now first spins, taking a unit that comes free as
`try_wait` takes one (only while the count is above zero, when no thread sleeps: a spinning thread never takes a unit a
sleeper was woken for), up to 200 pauses on x86 and 1,000 `isb` on ARM, then sleeps as before. SSRS2's report corpus
rendered as PDF on eight threads at once (720 documents): 6.8 to 7.0 s against 5.6 to 5.8 s (macOS arm64); a document
alone the same. The smoke test writes documents on eight threads at once, each the same bytes as one written alone.

Everything else is SkiaSharp's: its revision is in `skiasharp_revision` (SkiaSharp
[f568ac9](https://github.com/mono/SkiaSharp/commit/f568ac94dd768ef9a2f593537cfde2dd0d348ef5), the 3.119.4 release, and
the Skia it pins, [mono/skia 7dbfc07](https://github.com/mono/skia/commit/7dbfc07dd33181f84e0958afb7ee805c6c769f0b)),
and the library is built by its own scripts with their settings (`native/<platform>/build.cake`, run by Cake; on Linux
in its Docker images). Built so without the patch, the library wrote SSRS2's PDFs byte for byte as SkiaSharp's own
(macOS arm64).

## Building

```bash
./build.sh <work> macos            # osx: arm64 and x64 in one file (Xcode, .NET 10 SDK for Cake)
./build.sh <work> linux <arch>     # x64, arm64, arm: Debian 10's cross compilers in Docker (glibc 2.28 and later)
./build.sh <work> alpine <arch>    # x64, arm64: musl, in Alpine's
```

```powershell
./build.ps1 <work> <arch>          # x64, x86, arm64: LLVM's clang-cl, Visual Studio 2022
```

The library is in `<work>/out`. `test/Smoke` loads it with the `SkiaSharp` 3.119.4 package (copied over the package's
own library) and checks that a PDF's streams are deflated at the fastest level when the document names none, at the
level it names otherwise, that a run of text with its clusters says what a cluster of two glyphs reads, that documents
written on eight threads at once are each the bytes of one written alone (on 32-bit Windows some are not, with SkiaSharp's
own library too: counted, not failed; not run on 32-bit ARM, whose emulator in CI aborts on threads), and that it draws.

## Updating SkiaSharp

Put the new release's commit in `skiasharp_revision`: the patches are small and usually apply as they are.

## Platforms

osx (arm64 and x64 in one file), win-x64, win-x86, win-arm64, linux-x64, linux-arm64, linux-arm, linux-musl-x64,
linux-musl-arm64. The other runtimes SkiaSharp's packages carry (Linux on x86, RISC-V and LoongArch, Android's bionic)
keep SkiaSharp's own library. HarfBuzzSharp's library is not built here.

## Releases

A tag (`v3.119.4-ssrs2.1`...) builds the nine libraries, loads each on a machine of its platform (`test/Smoke`), and publishes them as a
release: a zip per runtime, their SHA-256 sums, and GitHub's build provenance attestation.

## Licences

This repository: MIT (`LICENSE`). SkiaSharp: MIT, © .NET Foundation and Contributors. Skia: BSD-3-Clause, © Google LLC.
The libraries carry both, as SkiaSharp's own packages do.
