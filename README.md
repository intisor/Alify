# Alify - Music Streaming Application

## 1. Project Overview

Alify is a music-centric web application built with ASP.NET Core Razor Pages. It integrates with the Spotify API for music playback and the Genius API for fetching song lyrics. The application presents lyrics in an innovative, user-friendly chat format, making it easy to follow along with songs, especially those with multiple artists.

### Key Technologies
- **Backend**: C#, ASP.NET Core, .NET 9
- **Frontend**: Razor Pages, HTML, CSS
- **APIs**: Spotify API, Genius API
- **IDE**: Visual Studio

---

## 2. Architecture and Communication Flow

The application follows a modern ASP.NET Core architecture that separates concerns into distinct layers, making the codebase modular, scalable, and easy to maintain.

### Core Components

| Component              | Responsibility                                                                                             |
| ---------------------- | ---------------------------------------------------------------------------------------------------------- |
| **Razor Pages (`.cshtml`)** | The presentation layer (UI). These files render the HTML that the user sees in their browser.              |
| **Page Models (`.cs`)**    | The backend logic for each Razor Page. They handle user input, orchestrate service calls, and prepare data for the view. |
| **Services**           | Reusable classes that encapsulate specific business logic, such as interacting with external APIs or parsing data. |
| **Models**             | Plain C# objects that define the data structures used throughout the application (e.g., `LyricMapping`).     |

### Communication Flow: The Lyrics Chat View Example

Here’s a step-by-step breakdown of how the components interact when a user visits the Lyrics View page:

1.  **HTTP Request**: The user navigates to the `/LyricsView` URL.
2.  **Page Model (`LyricsViewModel.cs`)**:
    *   ASP.NET Core routes the request to the `OnGetAsync` method in the `LyricsViewModel`.
    *   The Page Model calls the `SpotifyService` to get the user's currently playing track.
    *   It then calls the `LyricService` to fetch the raw lyrics for that track from the Genius API.
3.  **Lyric Parsing (`ArtistLyricService.cs`)**:
    *   The Page Model passes the raw lyrics to the `ArtistLyricService`.
    *   This service uses **regular expressions** to parse the text, identifying song sections (`[Chorus]`) and artist changes (`[Verse 1: Artist]`).
    *   It transforms the plain string into a structured `LyricMapping` object, where each line is associated with an artist and section.
4.  **Data Preparation**:
    *   The Page Model receives the structured `LyricMapping` object.
    *   It sets this object and the `MainArtist` as public properties, making them accessible to the view.
5.  **View Rendering (`LyricsView.cshtml`)**:
    *   The Razor engine renders the HTML. It accesses the `Model.LyricMapping` property.
    *   It **loops through each lyric line** and dynamically creates chat bubbles.
    *   **Conditional logic** is used to align bubbles to the right for the `MainArtist` and to the left for others, creating the chat-like appearance.
    *   Annotations are styled as centered "system messages."

This clean separation ensures that the UI (View) is decoupled from the business logic (Services), making the application robust and easier to debug.

---

## 3. In-Depth Feature Explanation

### The Lyrics Parsing Engine (`ArtistLyricService`)

This is the core of the lyrics feature.

-   **How it works**: It uses a state-machine-like approach. As it iterates through the lyric lines, it keeps track of the `currentArtist` and `currentSection`. When it encounters an annotation like `[Verse 2: Jinu]`, it updates its state, so all subsequent lines are correctly attributed to "Jinu" and "Verse 2".
-   **Why it's important**: This logic allows the application to understand the structure of a song with multiple performers, which is crucial for the chat-style display.

### The Lyrics Chat View (`LyricsView.cshtml`)

This feature provides a unique and intuitive way to read lyrics.

-   **Design Rationale**: A traditional block of text can be hard to follow, especially with multiple artists. A chat interface is a familiar paradigm that clearly separates speakers.
-   **Implementation**:
    *   The view uses the `MainArtist` property from its Page Model to decide the alignment of chat bubbles (`justify-content: flex-end` for the main artist, `flex-start` for others).
    *   The CSS is self-contained within the `.cshtml` file, making the component easy to understand and modify. The colors (`#056162` for the main artist, `#262d31` for others) are chosen to mimic popular messaging apps.

---

## 4. Setup and Running the Application

### Prerequisites
- .NET 9 SDK
- API keys for Spotify and Genius.

### Configuration
1.  **Clone the repository.**
2.  **Configure API Keys**: Store your API keys in `appsettings.json` or use User Secrets for better security:
    ```json
    {
      "Spotify": {
        "ClientId": "your_spotify_client_id",
        "ClientSecret": "your_spotify_client_secret",
        "RedirectUri": "http://127.0.0.1:7236/callback"
      },
      "Genius": {
        "token": "your_genius_api_token"
      }
    }
    ```
    *Note: For Spotify, ensure the `RedirectUri` is registered in your Spotify Developer Dashboard.*

### Running the App
1.  Open a terminal in the project root.
2.  Run `dotnet restore` to install dependencies.
3.  Run `dotnet run` to start the application.
4.  Navigate to `http://127.0.0.1:7236` in your browser.
5.  Log in with Spotify to enable all features.

---

## 5. How to Contribute

As a new developer, here are the key files to understand:

1.  **`Pages/LyricsView.cshtml`**: For UI and layout changes to the lyrics chat.
2.  **`Pages/LyricsView.cshtml.cs`**: For the logic that fetches and prepares data for the chat view.
3.  **`Services/ArtistLyricService.cs`**: For improving the lyric parsing engine.
4.  **`Program.cs`**: To understand how services are registered and configured at startup.

By reviewing the comments and structure of these files, you can get a solid understanding of the project's architecture and begin contributing effectively.