(() => {
    const focusTarget = document.querySelector('[data-focus-on-load="true"]');
    if (focusTarget instanceof HTMLElement) {
        focusTarget.focus({ preventScroll: false });
    }

    document.querySelectorAll('[data-toggle-password]').forEach((button) => {
        button.addEventListener('click', () => {
            const inputId = button.getAttribute('data-toggle-password');
            const input = inputId ? document.getElementById(inputId) : null;
            if (!(input instanceof HTMLInputElement)) {
                return;
            }

            const isPassword = input.type === 'password';
            input.type = isPassword ? 'text' : 'password';

            const textEl = button.querySelector('.hearth-auth-toggle-text');
            if (textEl) {
                textEl.textContent = isPassword ? 'Ocultar' : 'Mostrar';
            } else {
                button.textContent = isPassword ? 'Ocultar' : 'Mostrar';
            }

            const showLabel = button.getAttribute('data-label-show') || 'Mostrar senha';
            const hideLabel = button.getAttribute('data-label-hide') || 'Ocultar senha';

            button.classList.toggle('is-visible', isPassword);
            button.setAttribute('aria-pressed', isPassword ? 'true' : 'false');
            button.setAttribute('aria-label', isPassword ? hideLabel : showLabel);
        });
    });

    const resetFormSubmit = (form) => {
        const submit = form.querySelector('button[type="submit"]');
        if (!(submit instanceof HTMLButtonElement)) {
            return;
        }

        const originalText = submit.dataset.originalText;
        if (originalText) {
            const label = submit.querySelector('.hearth-auth-submit-label');
            if (label) {
                label.textContent = originalText;
            } else {
                submit.textContent = originalText;
            }
        }

        submit.disabled = false;
        submit.classList.remove('is-loading');
        submit.removeAttribute('aria-busy');
    };

    document.querySelectorAll('form[data-auth-form="true"]').forEach((form) => {
        const submit = form.querySelector('button[type="submit"]');
        if (submit instanceof HTMLButtonElement) {
            const label = submit.querySelector('.hearth-auth-submit-label');
            submit.dataset.originalText = (label ? label.textContent : submit.textContent)?.trim() || '';
        }

        form.addEventListener('submit', () => {
            if (!(submit instanceof HTMLButtonElement) || submit.disabled) {
                return;
            }

            submit.disabled = true;
            submit.classList.add('is-loading');
            submit.setAttribute('aria-busy', 'true');

            const loadingText = submit.getAttribute('data-loading-text');
            if (loadingText) {
                const label = submit.querySelector('.hearth-auth-submit-label');
                if (label) {
                    label.textContent = loadingText;
                } else {
                    submit.textContent = loadingText;
                }
            }
        });

        form.querySelectorAll('.hearth-auth-input.is-invalid').forEach((input) => {
            if (!(input instanceof HTMLInputElement)) {
                return;
            }

            input.addEventListener('input', () => {
                const value = input.value.trim();
                const isEmailField = input.type === 'email';
                const looksValid = isEmailField
                    ? value.includes('@') && value.includes('.') && !value.includes(' ')
                    : value.length > 0;

                if (!looksValid) {
                    return;
                }

                input.classList.remove('is-invalid');
                input.setAttribute('aria-invalid', 'false');

                const errorId = `${input.id}-error`;
                const errorEl = document.getElementById(errorId);
                if (errorEl) {
                    errorEl.hidden = true;
                }
            });
        });
    });

    window.addEventListener('pageshow', (event) => {
        if (!event.persisted) {
            return;
        }

        document.querySelectorAll('form[data-auth-form="true"]').forEach(resetFormSubmit);
    });
})();
