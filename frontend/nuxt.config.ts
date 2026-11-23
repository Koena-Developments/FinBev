const apiBase = process.env.NUXT_API_BASE || 'http://localhost:5280'

// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  app: {
    head: {
      script: [
        {
          key: 'finbev-theme',
          tagPriority: 'critical',
          innerHTML: "(function(){try{if(localStorage.getItem('finbev-theme')==='dark'){document.documentElement.classList.add('dark')}}catch(e){}})();"
        }
      ]
    }
  },
  devtools: { enabled: process.env.NODE_ENV === 'development' },
  modules: ['@nuxtjs/tailwindcss'],
  tailwindcss: {
    cssPath: '~/assets/css/main.css',
    configPath: 'tailwind.config.js'
  },
  runtimeConfig: {
    apiBase,
    proxySecret: process.env.NUXT_PROXY_SECRET || '',
    microsoftTenantId: process.env.NUXT_MICROSOFT_TENANT_ID || '',
    microsoftClientId: process.env.NUXT_MICROSOFT_CLIENT_ID || '',
    public: {
      microsoftSignIn:
        Boolean(process.env.NUXT_MICROSOFT_TENANT_ID) && Boolean(process.env.NUXT_MICROSOFT_CLIENT_ID),
      microsoftTenantId: process.env.NUXT_MICROSOFT_TENANT_ID || ''
    }
  },
  routeRules: {
    '/**': {
      headers: {
        'X-Frame-Options': 'DENY',
        'X-Content-Type-Options': 'nosniff',
        'Referrer-Policy': 'no-referrer',
        'Permissions-Policy': 'camera=(), microphone=(), geolocation=()'
      }
    }
  }
})
