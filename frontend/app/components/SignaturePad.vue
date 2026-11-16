<script setup lang="ts">
const props = defineProps<{ label?: string }>()
const model = defineModel<string | null>({ default: null })

const canvasRef = ref<HTMLCanvasElement | null>(null)
const drawing = ref(false)

onMounted(() => {
  const canvas = canvasRef.value
  if (!canvas) return
  const ctx = canvas.getContext('2d')
  if (!ctx) return
  ctx.fillStyle = '#fff'
  ctx.fillRect(0, 0, canvas.width, canvas.height)
  ctx.strokeStyle = '#1a4080'
  ctx.lineWidth = 2
  ctx.lineCap = 'round'
})

function pointerPos(e: PointerEvent) {
  const canvas = canvasRef.value!
  const rect = canvas.getBoundingClientRect()
  const scaleX = canvas.width / rect.width
  const scaleY = canvas.height / rect.height
  return {
    x: (e.clientX - rect.left) * scaleX,
    y: (e.clientY - rect.top) * scaleY
  }
}

function startDraw(e: PointerEvent) {
  drawing.value = true
  const ctx = canvasRef.value!.getContext('2d')!
  const p = pointerPos(e)
  ctx.beginPath()
  ctx.moveTo(p.x, p.y)
}

function draw(e: PointerEvent) {
  if (!drawing.value) return
  const ctx = canvasRef.value!.getContext('2d')!
  const p = pointerPos(e)
  ctx.lineTo(p.x, p.y)
  ctx.stroke()
}

function endDraw() {
  if (!drawing.value) return
  drawing.value = false
  model.value = canvasRef.value?.toDataURL('image/png') ?? null
}

function clear() {
  const canvas = canvasRef.value
  if (!canvas) return
  const ctx = canvas.getContext('2d')!
  ctx.fillStyle = '#fff'
  ctx.fillRect(0, 0, canvas.width, canvas.height)
  model.value = null
}
</script>

<template>
  <div class="space-y-2">
    <p v-if="props.label" class="text-sm font-medium text-foodbev-navy">{{ props.label }}</p>
    <div class="signature-sheet overflow-hidden rounded-xl border border-foodbev-mist bg-white">
      <canvas
        ref="canvasRef"
        class="block h-28 w-full touch-none cursor-crosshair"
        width="640"
        height="160"
        @pointerdown.prevent="startDraw"
        @pointermove.prevent="draw"
        @pointerup.prevent="endDraw"
        @pointerleave.prevent="endDraw"
      />
    </div>
    <button type="button" class="text-xs link-foodbev" @click="clear">Clear signature</button>
  </div>
</template>
