window.cultureInfo = window.cultureInfo || {
    getPreferredLanguage: function () {
        if (navigator.languages && navigator.languages.length > 0) {
            return navigator.languages[0];
        }

        return navigator.language || 'en';
    }
};
