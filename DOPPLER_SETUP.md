# Doppler Secrets Setup Guide

This project uses **Doppler** for secrets management. All API keys and sensitive configuration should be stored in Doppler, not in config files.

## Required Secrets in Doppler

Add the following secrets to your Doppler project:

### API Keys

```bash
# Gemini AI (Google)
ApiKeys__Gemini__ApiKey=your-gemini-api-key

# Genius Lyrics
ApiKeys__Genius__token=your-genius-token

# Mistral AI
ApiKeys__Mistral__ApiKey=your-mistral-api-key

# OpenRouter
ApiKeys__OpenRouter__ApiKey=your-openrouter-api-key
```

### Spotify Configuration

```bash
# Spotify API
Spotify__ClientId=your-spotify-client-id
Spotify__ClientSecret=your-spotify-client-secret
Spotify__RedirectUri=http://127.0.0.1:7236/callback
```

## How to Get API Keys

### 1. **Gemini API Key** (? Latest: `gemini-3-flash-preview`)
- Visit: https://aistudio.google.com/apikey
- Click "Get API Key"
- Create or select a project
- Copy your API key

### 2. **Genius API Token**
- Visit: https://genius.com/api-clients
- Create a new API client
- Generate an access token
- Copy the token

### 3. **Mistral API Key**
- Visit: https://console.mistral.ai/
- Sign up / Log in
- Go to API Keys section
- Generate a new key

### 4. **OpenRouter API Key**
- Visit: https://openrouter.ai/
- Sign up / Log in
- Go to Keys section
- Create a new API key

### 5. **Spotify API Credentials**
- Visit: https://developer.spotify.com/dashboard
- Create an app
- Get Client ID and Client Secret
- Set Redirect URI to: `http://127.0.0.1:7236/callback`

## Setup Instructions

### 1. Install Doppler CLI

**Windows:**
```powershell
# Using Scoop
scoop install doppler

# Or download from https://docs.doppler.com/docs/install-cli
```

**macOS/Linux:**
```bash
# macOS (Homebrew)
brew install dopplerhq/cli/doppler

# Linux
(curl -Ls --tlsv1.2 --proto "=https" https://cli.doppler.com/install.sh || wget -t 2 -qO- https://cli.doppler.com/install.sh) | sh
```

### 2. Login to Doppler

```bash
doppler login
```

### 3. Setup Project

```bash
cd Alify
doppler setup
```

### 4. Add Secrets

Option A: Via Doppler Dashboard
- Go to https://dashboard.doppler.com
- Select your project and environment
- Add secrets manually

Option B: Via CLI
```bash
doppler secrets set ApiKeys__Gemini__ApiKey="your-key"
doppler secrets set ApiKeys__Genius__token="your-token"
# ... etc
```

### 5. Run Your Application

The `DopplerConfigurationProvider` will automatically load secrets from Doppler when your app starts.

```bash
# Doppler token should be in environment variable
export DOPPLER_TOKEN=your-doppler-token

# Or run with doppler
doppler run -- dotnet run
```

## Security Notes

?? **NEVER commit API keys to Git!**
- ? `.gitignore` excludes all `appsettings.*.json` files
- ? All secrets should be in Doppler
- ? Appsettings files should only contain empty strings as placeholders

## Model Information

### Gemini Model
Currently using: **`gemini-3-flash-preview`** (latest as of Jan 2025)

Reference: https://ai.google.dev/gemini-api/docs/quickstart

## Troubleshooting

**Issue:** "API key not found"
- Ensure Doppler secrets are set correctly
- Check secret names match the expected format (with `__` separators)
- Verify `DOPPLER_TOKEN` environment variable is set

**Issue:** "Doppler connection failed"
- Check internet connection
- Verify Doppler CLI is logged in: `doppler whoami`
- Re-authenticate: `doppler login`
