<script setup lang="ts">
definePageMeta({ layout: false })

useHead({
  title: 'Sign in · FinBev',
  link: [
    {
      rel: 'stylesheet',
      href: 'https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap'
    }
  ]
})

const EMAIL_HINT_KEY = 'pettycash_last_email'

const email = ref('')
const password = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const rememberMe = ref(false)
const showPassword = ref(false)
const resetting = ref(false)
const error = ref('')
const submitting = ref(false)
const { markSignedIn } = useAuth()
const api = useApi()

if (import.meta.client) {
  const saved = localStorage.getItem(EMAIL_HINT_KEY)
  if (saved?.includes('@')) {
    email.value = saved
    rememberMe.value = true
  }
}

function showReset() {
  resetting.value = true
  error.value = ''
  password.value = ''
}

function showSignIn() {
  resetting.value = false
  error.value = ''
  newPassword.value = ''
  confirmPassword.value = ''
}

async function resetPassword() {
  error.value = ''
  if (newPassword.value !== confirmPassword.value) {
    error.value = 'The two passwords do not match.'
    return
  }
  submitting.value = true
  try {
    const result = await api<{ success: boolean; token: string | null; message: string | null }>(
      '/api/auth/reset-password',
      { method: 'POST', body: JSON.stringify({ email: email.value.trim(), newPassword: newPassword.value }) }
    )
    if (!result.success) {
      error.value = result.message || 'We could not reset your password. Try again.'
      return
    }
    markSignedIn()
    await navigateTo('/')
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'We could not reset your password. Try again.'
  } finally {
    submitting.value = false
  }
}

async function submit() {
  if (resetting.value) {
    await resetPassword()
    return
  }
  error.value = ''
  submitting.value = true
  try {
    const result = await api<{ success: boolean; mustChangePassword: boolean; token: string | null; message: string | null }>(
      '/api/auth/login',
      { method: 'POST', body: JSON.stringify({ email: email.value.trim(), password: password.value }) }
    )
    if (import.meta.client) {
      if (rememberMe.value) {
        localStorage.setItem(EMAIL_HINT_KEY, email.value.trim())
      } else {
        localStorage.removeItem(EMAIL_HINT_KEY)
      }
    }
    if (!result.success) {
      error.value = result.message || 'We could not sign you in. Check your email and password and try again.'
      return
    }
    if (result.mustChangePassword) {
      if (import.meta.client) sessionStorage.setItem('pettycash_pending_email', email.value.trim())
      await navigateTo('/change-password')
      return
    }
    markSignedIn()
    await navigateTo('/')
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'We could not sign you in. Check your email and password and try again.'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="finbev-frame">
    <div class="finbev-card">
      <section class="finbev-hero">
        <img src="/images/finbev-side.png" alt="" class="finbev-hero__art" />
        <div class="finbev-hero__copy">
          <h1>FinBev</h1>
          <p>Petty cash, simplified</p>
        </div>
      </section>

      <section class="finbev-panel">
        <form class="finbev-form" @submit.prevent="submit">
          <div class="finbev-theme">
            <ThemeToggle />
          </div>
          <img src="/images/foodbev-logo.png" alt="FoodBev Manufacturing SETA" class="finbev-logo" />
          <h2>{{ resetting ? 'Reset password' : 'Sign in' }}</h2>
          <p class="finbev-form__lead">{{ resetting ? 'Choose a new password for your work email.' : 'Welcome back to FinBev' }}</p>

          <label class="finbev-field" for="email">
            Email
            <span class="finbev-field__control">
              <input
                id="email"
                v-model="email"
                type="email"
                autocomplete="username"
                placeholder="Enter your email here"
                required
              />
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <rect x="3.5" y="5.5" width="17" height="13" rx="2" />
                <path d="m4 7 8 6 8-6" />
              </svg>
            </span>
          </label>

          <label v-if="!resetting" class="finbev-field" for="password">
            Password
            <span class="finbev-field__control">
              <input
                id="password"
                v-model="password"
                :type="showPassword ? 'text' : 'password'"
                autocomplete="current-password"
                placeholder="Enter your password"
                required
              />
              <button type="button" class="finbev-eye" :aria-pressed="showPassword" aria-label="Show password" @click="showPassword = !showPassword">
                <svg viewBox="0 0 24 24" aria-hidden="true">
                  <path d="M2.5 12S6.2 6.5 12 6.5 21.5 12 21.5 12 17.8 17.5 12 17.5 2.5 12 2.5 12Z" />
                  <circle cx="12" cy="12" r="2.6" />
                </svg>
              </button>
            </span>
          </label>

          <template v-if="resetting">
            <label class="finbev-field" for="new-password">
              New password
              <span class="finbev-field__control">
                <input
                  id="new-password"
                  v-model="newPassword"
                  :type="showPassword ? 'text' : 'password'"
                  autocomplete="new-password"
                  placeholder="At least 8 characters"
                  required
                />
                <button type="button" class="finbev-eye" :aria-pressed="showPassword" aria-label="Show password" @click="showPassword = !showPassword">
                  <svg viewBox="0 0 24 24" aria-hidden="true">
                    <path d="M2.5 12S6.2 6.5 12 6.5 21.5 12 21.5 12 17.8 17.5 12 17.5 2.5 12 2.5 12Z" />
                    <circle cx="12" cy="12" r="2.6" />
                  </svg>
                </button>
              </span>
            </label>
            <label class="finbev-field" for="confirm-password">
              Confirm password
              <span class="finbev-field__control">
                <input
                  id="confirm-password"
                  v-model="confirmPassword"
                  :type="showPassword ? 'text' : 'password'"
                  autocomplete="new-password"
                  placeholder="Type it again"
                  required
                />
              </span>
            </label>
          </template>

          <div class="finbev-row">
            <label v-if="!resetting" class="finbev-check">
              <input v-model="rememberMe" type="checkbox" />
              Remember me
            </label>
            <button v-if="resetting" type="button" class="finbev-link" @click="showSignIn">Back to sign in</button>
            <button v-else type="button" class="finbev-link" @click="showReset">Reset password</button>
          </div>

          <p v-if="error" class="finbev-error" role="alert">{{ error }}</p>

          <button type="submit" class="finbev-submit" :disabled="submitting">
            {{ submitting ? (resetting ? 'Saving…' : 'Signing in…') : (resetting ? 'Save new password' : 'Sign in') }}
          </button>
        </form>
      </section>
    </div>
  </div>
</template>

<style scoped>
.finbev-frame {
  --fb-blue: #1a4080;
  --fb-blue-mid: #3d6eb5;
  --fb-red: #e10600;
  --fb-gradient: linear-gradient(90deg, var(--fb-blue) 0%, var(--fb-blue-mid) 46%, var(--fb-red) 100%);
  position: relative;
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 28px;
  background: var(--fb-gradient);
  font-family: Inter, ui-sans-serif, system-ui, sans-serif;
}

.finbev-theme {
  display: flex;
  justify-content: flex-end;
  margin-bottom: 8px;
}

.finbev-card {
  width: min(1080px, 100%);
  min-height: 680px;
  display: grid;
  grid-template-columns: 1.05fr 0.95fr;
  gap: 8px;
  padding: 16px;
  border-radius: 28px;
  background: #fff;
  box-shadow: 0 24px 60px rgba(26, 64, 128, 0.22);
}

.finbev-hero {
  position: relative;
  overflow: hidden;
  border-radius: 22px;
  background: #1c2430;
  color: #fff;
}

.finbev-hero__copy {
  position: relative;
  z-index: 1;
  height: 100%;
  padding: 48px 32px 36px;
  text-align: center;
  background: linear-gradient(180deg, rgba(12, 22, 40, 0.62) 0%, rgba(12, 22, 40, 0.08) 38%, rgba(12, 22, 40, 0.28) 100%);
}

.finbev-hero__copy h1 {
  margin: 0;
  font-size: 40px;
  font-weight: 700;
  letter-spacing: -0.03em;
}

.finbev-hero__copy p {
  margin: 8px 0 0;
  font-size: 16px;
  font-weight: 500;
  color: rgba(255, 255, 255, 0.82);
}

.finbev-hero__art {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: cover;
  object-position: center 42%;
  pointer-events: none;
}

.finbev-panel {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 36px 28px;
  border-radius: 22px;
  background: #f7f7f8;
}

.finbev-form {
  width: min(360px, 100%);
}

.finbev-logo {
  display: block;
  height: 52px;
  width: auto;
  max-width: 220px;
  margin: 0 auto 22px;
  object-fit: contain;
}

.finbev-form h2 {
  margin: 0;
  text-align: center;
  font-size: 34px;
  font-weight: 800;
  letter-spacing: -0.04em;
  color: #1c1c1c;
}

.finbev-form__lead {
  margin: 8px 0 28px;
  text-align: center;
  font-size: 14px;
  color: #9aa0a8;
}

.finbev-field {
  display: block;
  margin-bottom: 16px;
  font-size: 14px;
  font-weight: 600;
  color: #2c2c2c;
}

.finbev-field__control {
  position: relative;
  display: block;
  margin-top: 8px;
}

.finbev-field input {
  width: 100%;
  height: 48px;
  padding: 0 44px 0 14px;
  border: 1px solid #e6e6ea;
  border-radius: 12px;
  background: #fff;
  font-size: 14px;
  color: #1c1c1c;
  outline: none;
}

.finbev-field input::placeholder {
  color: #b0b4bc;
}

.finbev-field input:focus {
  border-color: var(--fb-blue);
  box-shadow: 0 0 0 3px rgba(26, 64, 128, 0.16);
}

.finbev-field__control svg,
.finbev-eye svg {
  width: 18px;
  height: 18px;
  fill: none;
  stroke: #b0b4bc;
  stroke-width: 1.7;
  stroke-linecap: round;
  stroke-linejoin: round;
}

.finbev-field__control > svg {
  position: absolute;
  top: 15px;
  right: 14px;
}

.finbev-eye {
  position: absolute;
  top: 6px;
  right: 6px;
  display: grid;
  width: 36px;
  height: 36px;
  place-items: center;
  border: 0;
  border-radius: 8px;
  background: transparent;
  cursor: pointer;
}

.finbev-eye:hover svg {
  stroke: var(--fb-blue);
}

.finbev-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin: 4px 0 18px;
  font-size: 13px;
}

.finbev-check {
  display: flex;
  align-items: center;
  gap: 8px;
  color: #6b7280;
  font-weight: 500;
  cursor: pointer;
}

.finbev-check input {
  accent-color: var(--fb-blue);
}

.finbev-link {
  margin-left: auto;
  padding: 0;
  border: 0;
  background: transparent;
  color: var(--fb-blue);
  font: inherit;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
}

.finbev-link:hover {
  text-decoration: underline;
}

.finbev-error {
  margin: 0 0 12px;
  font-size: 13px;
  font-weight: 600;
  color: #e11d48;
}

.finbev-submit {
  width: 100%;
  height: 50px;
  border: 0;
  border-radius: 12px;
  background: var(--fb-gradient);
  color: #fff;
  font-size: 15px;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.15s ease;
}

.finbev-submit:hover:not(:disabled) {
  filter: brightness(0.94);
}

.finbev-submit:disabled {
  cursor: progress;
  opacity: 0.75;
}

:global(html.dark .finbev-frame) {
  background: linear-gradient(90deg, #0c1830 0%, #163566 50%, #8f140c 100%);
}

:global(html.dark .finbev-card) {
  background: #121e33;
  box-shadow: 0 24px 60px rgba(0, 0, 0, 0.35);
}

:global(html.dark .finbev-panel) {
  background: #182842;
}

:global(html.dark .finbev-logo) {
  border-radius: 12px;
  background: #f4f8fc;
  padding: 8px 12px;
}

:global(html.dark .finbev-form h2),
:global(html.dark .finbev-field) {
  color: #e8f0fb;
}

:global(html.dark .finbev-form__lead),
:global(html.dark .finbev-check) {
  color: #9eb0cc;
}

:global(html.dark .finbev-field input) {
  border-color: rgba(158, 197, 239, 0.24);
  background: #102038;
  color: #e8f0fb;
}

:global(html.dark .finbev-field input::placeholder),
:global(html.dark .finbev-field__control > svg),
:global(html.dark .finbev-eye svg) {
  color: #8ea3c2;
  stroke: #8ea3c2;
}

:global(html.dark .finbev-row a) {
  color: #c5ddf7;
}

@media (max-width: 860px) {
  .finbev-frame {
    padding: 16px;
  }

  .finbev-card {
    grid-template-columns: 1fr;
    min-height: 0;
  }

  .finbev-hero {
    min-height: 280px;
  }

  .finbev-hero__copy {
    padding-top: 28px;
  }

  .finbev-hero__copy h1 {
    font-size: 32px;
  }
}
</style>
