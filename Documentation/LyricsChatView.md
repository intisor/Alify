# Lyrics Chat View Feature

## Overview
The Lyrics Chat View presents song lyrics in a beautiful, WhatsApp-like chat interface where each artist's sections appear as individual chat messages.

## Features

### 🎨 **Chat-Style Interface**
- **Artist Avatars**: Colorful circular avatars with artist initials
- **Message Bubbles**: Clean, modern message bubbles for each lyric section
- **Line Numbers**: Shows the line range for each section
- **Section Labels**: Displays section types (Verse 1, Chorus, Bridge, etc.)

### 🎵 **Smart Lyrics Parsing**
- Automatically detects artist annotations like `[Verse 1: Artist]`
- Groups consecutive lines by the same artist into single messages
- Maintains proper line numbering throughout the song
- Handles various annotation formats (Verse, Chorus, Bridge, Pre-Chorus, Post-Chorus, etc.)

### 📱 **Responsive Design**
- Mobile-friendly interface
- Smooth animations and transitions
- Custom scrollbar styling
- Gradient background for visual appeal

## How to Use

### 1. **Access the View**
Navigate to `/LyricsView` or click "Lyrics Chat" in the navigation menu.

### 2. **Search for Lyrics**
- Enter artist name and song title in the search form at the bottom
- Click "Search" to fetch and display lyrics

### 3. **Demo Mode**
- Visit `/LyricsView` without parameters to see demo lyrics
- Shows sample conversation between "Rumi" and "Jinu"

## Example Interface

```
🎵 Sample Song
by Demo

┌─────────────────────────────────────┐
│ RU  Rumi                           │
│     Verse 1 • Lines 1-8           │
│ ┌─────────────────────────────────┐ │
│ │ I tried to hide but something   │ │
│ │ broke                           │ │
│ │ I tried to sing, couldn't hit   │ │
│ │ the notes...                    │ │
│ └─────────────────────────────────┘ │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│ RU  Rumi                           │
│     Chorus • Lines 10-17          │
│ ┌─────────────────────────────────┐ │
│ │ Why does it feel right every    │ │
│ │ time I let you in?              │ │
│ │ Why does it feel like I can     │ │
│ │ tell you anything?...           │ │
│ └─────────────────────────────────┘ │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│ JI  Jinu                           │
│     Verse 2 • Lines 24-27         │
│ ┌─────────────────────────────────┐ │
│ │ Ooh, time goes by, and I lose   │ │
│ │ perspective...                   │ │
│ └─────────────────────────────────┘ │
└─────────────────────────────────────┘
```

## Technical Implementation

### Backend (LyricsViewModel)
- Integrates with `ArtistLyricService` for lyrics parsing
- Integrates with `LyricService` for fetching lyrics from external APIs
- Converts parsed lyrics into `ChatMessage` objects
- Handles both real API calls and demo data

### Frontend (LyricsView.cshtml)
- Responsive CSS with Bootstrap integration
- Custom animations and hover effects
- Color-coded artist avatars with automatic color assignment
- Font Awesome icons for enhanced UI

### Models
- `ChatMessage`: Represents a conversation message with artist, section, content, and line numbers
- `LyricMapping`: Contains parsed lyrics with artist and section mappings
- `LyricLine`: Individual line with metadata

## Color Coding
Each artist gets assigned a unique color from a predefined palette:
- Colors are consistently assigned based on artist name hash
- Ensures the same artist always gets the same color
- 10 beautiful colors available for variety

## Navigation
- Added "Lyrics Chat" link to the main navigation
- Accessible from any page in the application
- Clean, intuitive search interface

## Integration with Existing Services
- Uses existing `LyricService` for fetching lyrics from Genius API
- Uses `ArtistLyricService` for parsing and line mapping
- Leverages existing dependency injection setup
- Compatible with current lyrics moderation system

## Future Enhancements
- Real-time lyrics following during song playback
- User favorite lyrics sections
- Lyrics sharing functionality
- Multiple language support
- Voice annotation playback
- Collaborative lyrics editing

This feature transforms the traditional static lyrics display into an engaging, modern chat-like experience that makes following along with multi-artist songs much more intuitive and enjoyable!
