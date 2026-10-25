# Alify — Bring Your Own Key (BYOK) Setup Guide

Alify is designed with a **privacy-first, decentralized BYOK architecture**. You do not need to rely on a central server, hosted credentials, or third-party subscription proxies.

This guide walks you through setting up:
1. [Spotify Developer Client ID](#part-1-spotify-developer-client-id) (avoids Spotify's 25-user development mode cap)
2. [AI Provider API Keys](#part-2-ai-semantic-analysis-keys) (Google Gemini, OpenRouter, Groq, or Local Ollama)

---

## Part 1: Spotify Developer Client ID

Spotify limits developer applications in development mode to a small whitelist of authorized accounts. To use Alify without account restrictions, you can provide your own personal Spotify Developer Client ID.

> [!NOTE]
> Alify uses the **OAuth 2.0 PKCE** flow (Proof Key for Code Exchange). **You only need your Client ID.** You do **not** need (and should never share) your Spotify Client Secret.

### Step-by-Step Setup

1. **Open the Spotify Developer Dashboard**:
   Navigate to [developer.spotify.com/dashboard](https://developer.spotify.com/dashboard) and log in with your Spotify account.

2. **Create an Application**:
   - Click the **Create App** button in the top right.
   - **App Name**: `Alify` (or any custom name)
   - **App Description**: `Personal music filter and monitor`
   - **Which API/SDKs are you planning to use?**: Select **Web API**.

3. **Configure Redirect URIs**:
   In the **Redirect URIs** field, add the following URLs depending on which Alify clients you use:
   - For **Alify Desktop / Web**:
     ```text
     http://127.0.0.1:5001/callback
     https://127.0.0.1:5001/callback
     ```
   - For **Alify Android**:
     ```text
     alify://callback
     ```
   Click **Add** for each URI, then check the developer terms checkbox and click **Save**.

4. **Copy Your Client ID**:
   - In your newly created app's overview page, click **Settings**.
   - Copy the string under **Client ID** (e.g., `4a7b9c...`).

5. **Paste into Alify**:
   - **Desktop**: On the Alify login screen, paste your Client ID into the **"Bring Your Own Keys: Spotify Client ID"** field before clicking **Connect with Spotify**.
   - **Android**: Tap the **Settings (⚙️)** gear icon in the top right, and paste your Client ID under **Spotify Developer Client ID (BYOK)**. The app includes direct in-app buttons to open the dashboard and copy the redirect URI with one tap.

---

## Part 2: AI Semantic Analysis Keys

Alify uses AI models to de-cloak coded slang, multi-lingual double entendres (e.g. Yoruba, Nigerian Pidgin, UK drill), and implicit content that standard keyword filters miss. 

You can use **any** of the following supported AI backends:

| Provider | Recommended Model | Pricing | API Key Prefix | Best For |
| :--- | :--- | :--- | :--- | :--- |
| **Google Gemini** | `gemini-2.5-flash` | Free tier available | `AIza...` | Default, highest contextual nuance |
| **OpenRouter** | `meta-llama/llama-3.3-70b-instruct:free` | Free models available | `sk-or-v1-...` | Universal multi-model gateway |
| **Groq** | `llama-3.3-70b-versatile` | Free tier available | `gsk_...` | Ultra-fast inference (< 1 sec) |
| **Local Ollama** | `llama3.3` / `mistral` | 100% Free & Offline | None required | Total privacy, zero internet calls |

---

### Option A: Google Gemini (Recommended)

1. Go to [Google AI Studio](https://aistudio.google.com/).
2. Sign in with your Google account.
3. Click **Get API key** in the sidebar, then click **Create API key**.
4. Copy the generated key (starts with `AIza...`).
5. Paste it into Alify:
   - **Desktop**: In the Lyrics Semantic Analysis screen, expand *Optional: Run AI BYOK Model* and paste the key.
   - **Android**: Tap the **Settings (⚙️)** icon, enable AI semantic scanning, and enter your key under **AI API Key (BYOK)**.

---

### Option B: OpenRouter (Free Multi-Provider Models)

OpenRouter gives you access to open-source and proprietary models through a single API key, including completely free Llama 3.3 tiers.

1. Sign up at [openrouter.ai](https://openrouter.ai/).
2. Go to [openrouter.ai/keys](https://openrouter.ai/keys) and click **Create Key**.
3. Copy your key (starts with `sk-or-...`).
4. Paste it into Alify's AI API Key field. Alify automatically detects `sk-` keys and routes to OpenRouter using `meta-llama/llama-3.3-70b-instruct:free`.

---

### Option C: Groq (Ultra-Fast Free Llama 3.3)

Groq provides lightning-fast inference on LPUs (Language Processing Units).

1. Go to [console.groq.com](https://console.groq.com/).
2. Create an account and navigate to **API Keys**.
3. Click **Create API Key** and copy it (starts with `gsk_...`).
4. Paste it into Alify's AI API Key field. Alify automatically detects `gsk_` keys and routes to Groq using `llama-3.3-70b-versatile`.

---

### Option D: Local Ollama (100% Offline & Private)

If you do not want your lyrics sent to any cloud provider, run an LLM locally on your own GPU/CPU:

1. Install [Ollama](https://ollama.ai/).
2. Pull a recommended instruction-tuned model:
   ```bash
   ollama run llama3.3
   ```
3. Ollama runs an OpenAI-compatible REST server at `http://localhost:11434/v1/chat/completions`.
4. Alify can communicate with this endpoint directly without needing an API key.

---

## Part 3: Privacy & Security Guarantees

- **No Secret Storage**: Spotify Client Secrets are never requested or stored.
- **Isolated Lyric Transmissions**: When an AI audit runs, **only** the normalized, de-identified lyric text is transmitted. Your Spotify auth tokens, user ID, email, and playback history are **never** sent to any AI provider.
- **Strict Ledger Audit**: Every finding is verifiable down to the exact line number citations in the local SQLite ledger.
