# ArtistLyricService Documentation

## Overview
The `ArtistLyricService` is designed to parse song lyrics, number the lines, and identify artist annotations and their mappings to specific lines. It uses regular expressions to detect artist notations in brackets and maps them to line numbers.

## Features

### 1. Line Numbering
- Automatically numbers all lines in the lyrics (starting from 1)
- Handles empty lines appropriately
- Provides formatted output with line numbers

### 2. Artist Annotation Detection
- Detects patterns like `[Verse 1: Artist]`, `[Chorus: Artist]`, etc.
- Supports both section-only annotations `[Chorus]` and artist-specific annotations `[Verse 1: Artist]`
- Maintains context across multiple lines until a new annotation is found

### 3. Line Mapping
- Maps artists to their corresponding line numbers
- Maps sections (Verse, Chorus, Bridge, etc.) to their line numbers
- Provides both annotation lines and lyric content lines for each artist/section

## Supported Annotation Formats

- `[Verse 1: Artist]` - Verse with specific artist
- `[Chorus: Artist]` - Chorus with specific artist
- `[Bridge]` - Section without specific artist
- `[Pre-Chorus: Artist]` - Pre-chorus with artist
- `[Post-Chorus: Artist]` - Post-chorus with artist
- `[Outro: Artist]` - Outro with artist
- `[Intro]` - Intro section
- And other similar patterns

## Methods

### `ParseLyricsWithArtistMapping(string lyrics)`
Returns a complete `LyricMapping` object containing:
- All lines with their numbers, text, artist, and section information
- Artist-to-line-numbers mapping
- Section-to-line-numbers mapping

### `GetNumberedLyrics(string lyrics)`
Returns the lyrics with line numbers (001:, 002:, etc.)

### `GetArtistsInLyrics(string lyrics)`
Returns a list of all artists found in the lyrics annotations

### `GetLineNumbersForArtist(string artist, string lyrics)`
Returns all line numbers where the specified artist sings

### `GetLineNumbersForSection(string section, string lyrics)`
Returns all line numbers for a specific section (e.g., "Verse 1", "Chorus")

### `GetArtistLineMapping(string lyrics)`
Returns a dictionary mapping artist names to their line numbers

### `GetSectionLineMapping(string lyrics)`
Returns a dictionary mapping section names to their line numbers

### `GetLyricLinesForArtist(string lyrics, string artist)`
Returns detailed line information for a specific artist

### `GetFormattedLyricsWithMapping(string lyrics)`
Returns formatted lyrics with line numbers and artist/section information

## Example Usage

```csharp
var artistLyricService = new ArtistLyricService();

var lyrics = @"[Verse 1: Rumi]
I tried to hide but something broke
I tried to sing, couldn't hit the notes

[Chorus: Rumi]
Why does it feel right every time I let you in?
Why does it feel like I can tell you anything?

[Verse 2: Jinu]
Ooh, time goes by, and I lose perspective
Yeah, hope only hurts, so I just forget it";

// Get numbered lyrics
var numbered = artistLyricService.GetNumberedLyrics(lyrics);

// Get all artists
var artists = artistLyricService.GetArtistsInLyrics(lyrics);
// Result: ["Rumi", "Jinu"]

// Get line numbers for specific artist
var rumiLines = artistLyricService.GetLineNumbersForArtist(lyrics, "Rumi");
// Result: [1, 2, 3, 4, 5, 6]

// Get artist mapping
var mapping = artistLyricService.GetArtistLineMapping(lyrics);
// Result: {"Rumi": [1,2,3,4,5,6], "Jinu": [7,8,9]}
```

## API Endpoints

### `POST /api/spotify/test-lyrics-parsing`
Demonstrates the service functionality with sample lyrics

### `GET /api/spotify/lyrics-for-artist/{artist}`
Returns line numbers and lyric lines for a specific artist

## Models

### `LyricLine`
- `LineNumber`: The line number in the lyrics (1-based)
- `Text`: The actual text content of the line
- `Artist`: The artist associated with this line (null if none)
- `Section`: The section this line belongs to (e.g., "Verse 1", "Chorus")
- `IsAnnotation`: Whether this line is an annotation (e.g., "[Verse 1: Artist]") or actual lyrics

### `LyricMapping`
- `Lines`: List of all `LyricLine` objects
- `ArtistToLineNumbers`: Dictionary mapping artist names to their line numbers
- `SectionToLineNumbers`: Dictionary mapping section names to their line numbers

## Integration

The service is registered as a singleton in the DI container and can be injected into controllers, services, or other components that need lyrics parsing functionality.

This service integrates well with the existing `LyricService` which fetches lyrics from external sources like Genius API, allowing you to first fetch the lyrics and then parse them for artist attribution.
