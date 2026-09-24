window.stepMedia = {
    observer: null,

    init() {
        if (this.observer) {
            this.observer.disconnect();
            this.observer = null;
        }

        const items = document.querySelectorAll('.article-body [data-step-id]');
        const frames = document.querySelectorAll('.step-media-rail .step-media-frame');
        if (!items.length || !frames.length) {
            return;
        }

        const setActive = (id) => {
            frames.forEach(f => f.classList.toggle('is-active', f.dataset.stepId === id));
        };

        // Sensible default while the user hasn't scrolled: whichever frame is
        // already marked is-active server-side, else the first frame.
        const preActive = Array.from(frames).find(f => f.classList.contains('is-active'));
        setActive((preActive || frames[0]).dataset.stepId);

        this.observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const hasFrame = Array.from(frames).some(f => f.dataset.stepId === entry.target.dataset.stepId);
                    if (hasFrame) {
                        setActive(entry.target.dataset.stepId);
                    }
                }
            });
        }, {
            root: null,
            // Treat a step as "current" once it's crossed roughly the upper-middle of the viewport.
            rootMargin: '-15% 0px -70% 0px',
            threshold: 0
        });

        items.forEach(el => this.observer.observe(el));
    }
};
