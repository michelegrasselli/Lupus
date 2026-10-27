window.lupusStorage = {
    save: function (key, value) {
        try {
            localStorage.setItem(key, value);
            return true;
        } catch (e) {
            return false;
        }
    },

    load: function (key) {
        try {
            return localStorage.getItem(key);
        } catch (e) {
            return null;
        }
    },

    remove: function (key) {
        try {
            localStorage.removeItem(key);
        } catch (e) {
            // Ignora: il browser potrebbe bloccare lo storage.
        }
    },

    download: function (filename, content) {
        const blob = new Blob([content], { type: 'application/json;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(url);
    },

    beep: function () {
        try {
            const AudioCtx = window.AudioContext || window.webkitAudioContext;
            if (!AudioCtx) return;
            const ctx = new AudioCtx();
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();
            osc.type = 'sine';
            osc.frequency.setValueAtTime(880, ctx.currentTime);
            gain.gain.setValueAtTime(0.0001, ctx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.25, ctx.currentTime + 0.02);
            gain.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + 0.9);
            osc.connect(gain);
            gain.connect(ctx.destination);
            osc.start();
            osc.stop(ctx.currentTime + 0.95);
            osc.onended = function () { ctx.close(); };
        } catch (e) {
            // Il browser può bloccare l'audio senza interazione utente.
        }
    },

    scrollLog: function () {
        const el = document.querySelector('.log-list');
        if (el) el.scrollTop = el.scrollHeight;
    }
};
