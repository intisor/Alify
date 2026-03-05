// Genius Lyrics Search JavaScript (Issue #2 - Simplified)
// Handles search form submission, API calls, result display, and navigation to lyrics

(function() {
    'use strict';

    const API_BASE = '/api/lyrics/search';

    // DOM elements
    const searchForm = document.getElementById('searchForm');
    const loadingState = document.getElementById('loadingState');
    const errorState = document.getElementById('errorState');
    const errorMessage = document.getElementById('errorMessage');
    const searchResults = document.getElementById('searchResults');
    const resultsList = document.getElementById('resultsList');

    if (!searchForm) {
        console.info('Search form not found - probably viewing lyrics already');
        return;
    }

    /**
     * Handle search form submission
     */
    searchForm.addEventListener('submit', async (e) => {
        e.preventDefault();
        
        const formData = new FormData(searchForm);
        const query = formData.get('query');
        const artist = formData.get('artist');

        if (!query) {
            showError('Please enter a song title');
            return;
        }

        await searchSongs(query, artist);
    });

    /**
     * Search for songs via the API
     */
    async function searchSongs(query, artist) {
        hideAll();
        loadingState.classList.remove('hidden');

        try {
            const params = new URLSearchParams({ query });
            if (artist) params.append('artist', artist);

            const response = await fetch(`${API_BASE}?${params.toString()}`);
            
            if (!response.ok) {
                throw new Error(`Search failed: ${response.status} ${response.statusText}`);
            }

            const data = await response.json();
            displayResults(data);
        } catch (error) {
            console.error('Search error:', error);
            showError(error.message || 'Failed to search. Please try again.');
        } finally {
            loadingState.classList.add('hidden');
        }
    }

    /**
     * Display search results
     */
    function displayResults(response) {
        if (!response.results || response.results.length === 0) {
            showError('No songs found. Try different search terms.');
            return;
        }

        resultsList.innerHTML = '';

        response.results.forEach(result => {
            const card = createResultCard(result);
            resultsList.appendChild(card);
        });

        searchResults.classList.remove('hidden');
    }

    /**
     * Create a search result card
     */
    function createResultCard(result) {
        const card = document.createElement('div');
        card.className = 'border border-gray-200 rounded-lg p-4 hover:border-blue-500 hover:shadow-md transition cursor-pointer';
        card.onclick = () => loadLyrics(result);

        const thumbnail = result.thumbnailUrl
            ? `<img src="${result.thumbnailUrl}" alt="${result.title}" class="w-16 h-16 rounded object-cover mr-4" />`
            : '<div class="w-16 h-16 bg-gray-200 rounded mr-4 flex items-center justify-center"><span class="text-gray-400 text-2xl">♪</span></div>';

        card.innerHTML = `
            <div class="flex items-start">
                ${thumbnail}
                <div class="flex-1">
                    <h4 class="font-semibold text-lg text-gray-900">${escapeHtml(result.title)}</h4>
                    <p class="text-sm text-gray-600">${escapeHtml(result.artist)}</p>
                    ${result.album ? `<p class="text-xs text-gray-500 mt-1">📀 ${escapeHtml(result.album)}</p>` : ''}
                    ${result.releaseDateDisplay ? `<p class="text-xs text-gray-400 mt-1">📅 ${escapeHtml(result.releaseDateDisplay)}</p>` : ''}
                </div>
                <svg class="w-5 h-5 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"></path>
                </svg>
            </div>
        `;

        return card;
    }

    /**
     * Navigate to lyrics view with selected song
     * The existing LyricsViewModel.OnGetAsync(artist, track) will handle the rest
     */
    function loadLyrics(result) {
        const params = new URLSearchParams({
            artist: result.artist,
            track: result.title
        });
        
        // Reload page with query params - OnGetAsync will fetch and display lyrics
        window.location.href = `/LyricsView?${params.toString()}`;
    }

    /**
     * Show error message
     */
    function showError(message) {
        hideAll();
        errorMessage.textContent = message;
        errorState.classList.remove('hidden');
    }

    /**
     * Hide all state containers
     */
    function hideAll() {
        loadingState.classList.add('hidden');
        errorState.classList.add('hidden');
        searchResults.classList.add('hidden');
    }

    /**
     * Escape HTML to prevent XSS
     */
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
})();
