/** @type {import('tailwindcss').Config} */
export default {
  content: [
    './app/**/*.{vue,js,ts}',
    './app/assets/css/**/*.css',
    './components/**/*.{vue,js,ts}'
  ],
  theme: {
    extend: {
      colors: {
        foodbev: {
          navy: '#1a4080',
          'navy-dark': '#163566',
          blue: '#3d6eb5',
          sky: '#4d8fd1',
          'sky-light': '#5b9fd4',
          ice: '#9ec5ef',
          mist: '#e8f0fa',
          paper: '#f4f8fc'
        }
      }
    }
  },
  plugins: []
}
