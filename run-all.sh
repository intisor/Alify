#!/bin/bash

# Run all Alify projects in parallel (Linux/macOS)

echo "Starting all Alify projects..."
echo ""

# Define project info
LYRICS_PROJECT="./Alify.Lyrics"
SPOTIFY_PROJECT="./Alify.Spotify"
LYRICS_PORT=5000
SPOTIFY_PORT=5001

# Start each project in background
echo "Starting Alify.Lyrics on port $LYRICS_PORT..."
dotnet run --project $LYRICS_PROJECT &
LYRICS_PID=$!

sleep 1

echo "Starting Alify.Spotify on port $SPOTIFY_PORT..."
dotnet run --project $SPOTIFY_PROJECT &
SPOTIFY_PID=$!

echo ""
echo "All projects started successfully!"
echo ""
echo "Services running at:"
echo "  - Alify.Lyrics:  http://localhost:$LYRICS_PORT"
echo "  - Alify.Spotify: http://localhost:$SPOTIFY_PORT"
echo ""
echo "Process IDs: Lyrics=$LYRICS_PID, Spotify=$SPOTIFY_PID"
echo "Press Ctrl+C to stop all services..."
echo ""

# Cleanup on exit