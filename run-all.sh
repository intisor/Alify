#!/bin/bash
# Run all Alify projects in parallel with coordinated loopback port binding (ALI-026)

echo "Starting all Alify projects..."
echo ""

LYRICS_PROJECT="./Alify.Lyrics"
SPOTIFY_PROJECT="./Alify.Spotify"
LYRICS_PORT=5000
SPOTIFY_PORT=5001

# Start each project in background with explicit loopback URLs
echo "Starting Alify.Lyrics on http://127.0.0.1:$LYRICS_PORT..."
dotnet run --project $LYRICS_PROJECT --no-launch-profile --urls http://127.0.0.1:$LYRICS_PORT &
LYRICS_PID=$!

sleep 1

echo "Starting Alify.Spotify on http://127.0.0.1:$SPOTIFY_PORT..."
dotnet run --project $SPOTIFY_PROJECT --no-launch-profile --urls http://127.0.0.1:$SPOTIFY_PORT &
SPOTIFY_PID=$!

echo ""
echo "All projects started successfully!"
echo ""
echo "Services running at:"
echo "  - Alify.Lyrics:  http://127.0.0.1:$LYRICS_PORT"
echo "  - Alify.Spotify: http://127.0.0.1:$SPOTIFY_PORT"
echo ""
echo "Process IDs: Lyrics=$LYRICS_PID, Spotify=$SPOTIFY_PID"
echo "Press Ctrl+C to stop all services..."
echo ""

# Cleanup on exit
trap "kill $LYRICS_PID $SPOTIFY_PID 2>/dev/null; exit" SIGINT SIGTERM
wait