<script setup lang="ts">
import { formatZar } from '~/utils/format'

export type PettyCashFormData = {
  displayId: string
  claimantName: string
  departmentName: string
  businessUnitName: string
  budgetCode: string
  amount: number
  reason: string
  expenseDate: string
  claimantSignedAtUtc: string | null
  claimantSignaturePngBase64: string | null
  departmentManagerSignedAtUtc: string | null
  departmentManagerSignaturePngBase64: string | null
  departmentManagerName: string | null
  financeManagerSignedAtUtc: string | null
  financeManagerSignaturePngBase64: string | null
  financeManagerName: string | null
}

defineProps<{ form: PettyCashFormData }>()

function sigSrc(base64: string | null) {
  if (!base64) return null
  return base64.startsWith('data:') ? base64 : `data:image/png;base64,${base64}`
}

function fmtDate(iso: string | null) {
  if (!iso) return '____ / ____ / ________'
  const d = new Date(iso)
  const dd = String(d.getDate()).padStart(2, '0')
  const mm = String(d.getMonth() + 1).padStart(2, '0')
  const yyyy = d.getFullYear()
  return `${dd} / ${mm} / ${yyyy}`
}

function amountWords(amount: number) {
  return formatZar(amount)
}
</script>

<template>
  <article class="petty-cash-form-official relative mx-auto max-w-[820px] bg-white px-10 py-12 text-[11pt] leading-relaxed text-black shadow-md print:shadow-none">
    <img
      src="/images/petty-cash-form-watermark.jpg"
      alt=""
      class="pointer-events-none absolute right-0 top-0 h-40 w-auto max-w-[45%] object-contain opacity-90 print:opacity-100"
      aria-hidden="true"
    />

    <header class="relative z-10 mb-8 text-center">
      <h1 class="text-[20pt] font-bold tracking-wide">PETTY CASH FORM</h1>
      <p class="mt-2 text-[11pt] font-bold">Petty cash voucher No: {{ form.displayId || '………………' }}</p>
      <p class="mt-1 text-[11pt] font-bold">Petty Cash Claims up to R2 000.00</p>
      <p class="mt-1 text-[11pt] font-bold">(An invoice / receipt must be submitted within 3 days)</p>
    </header>

    <section class="relative z-10 space-y-4">
      <p class="font-bold underline">TO BE FILLED IN BY REQUESTOR OF PETTY CASH</p>
      <p class="min-h-[1.5em]">
        Department <span class="font-semibold underline decoration-dotted">{{ form.departmentName || '………………………………' }}</span>
        <span class="inline-block w-6" />
        Cost Centre <span class="font-semibold underline decoration-dotted">{{ form.businessUnitName || '………………' }}</span>
        <span v-if="form.budgetCode" class="text-[10pt]"> ({{ form.budgetCode }})</span>
      </p>
      <p>
        Claimed By <span class="font-semibold underline decoration-dotted">{{ form.claimantName || '………………………………………' }}</span>
      </p>
      <p>
        Amount <span class="font-semibold underline decoration-dotted">{{ form.amount ? amountWords(form.amount) : '…………………………………………………………….' }}</span>
      </p>
      <p>
        Reason for Claim
        <span class="block min-h-[2.5em] font-semibold underline decoration-dotted">{{ form.reason || '………………………………………………………………………………………' }}</span>
      </p>
      <p v-if="form.expenseDate" class="text-[10pt]">Expense date: {{ form.expenseDate }}</p>

      <div class="pt-4">
        <div class="flex flex-wrap items-end gap-8">
          <div class="min-w-[240px]">
            <span class="block text-[10pt]">Claimant Signature</span>
            <img
              v-if="sigSrc(form.claimantSignaturePngBase64)"
              :src="sigSrc(form.claimantSignaturePngBase64)!"
              alt="Claimant signature"
              class="mt-1 h-14 max-w-[220px] object-contain"
            />
            <div v-else class="mt-2 h-12 border-b border-black" />
          </div>
          <p class="font-bold">Date: {{ fmtDate(form.claimantSignedAtUtc) }}</p>
        </div>
      </div>

      <p class="pt-4">I hereby approve that the claim is for FoodBev Seta purposes and necessary.</p>

      <div class="flex flex-wrap items-end gap-8 pt-2">
        <div class="min-w-[240px]">
          <span class="block text-[10pt]">Senior Manager’s Signature</span>
          <img
            v-if="sigSrc(form.departmentManagerSignaturePngBase64)"
            :src="sigSrc(form.departmentManagerSignaturePngBase64)!"
            alt="Department senior manager signature"
            class="mt-1 h-14 max-w-[220px] object-contain"
          />
          <div v-else class="mt-2 h-12 border-b border-black" />
          <p v-if="form.departmentManagerName" class="text-[10pt]">{{ form.departmentManagerName }}</p>
        </div>
        <p class="font-bold">Date: {{ fmtDate(form.departmentManagerSignedAtUtc) }}</p>
      </div>

      <p class="pt-6 font-bold underline">The Cashier</p>
      <p>Invoice/ Receipt Amount …………………………………….</p>
      <p>(Receipts/Invoices attached)</p>
      <p class="pt-2">Change Returned ………………………………………</p>
      <p class="pt-2">Signature …………………………………….</p>
      <p>Date …………. /…………….../............</p>

      <p class="pt-6 font-bold underline">TO BE FILLED IN BY REQUESTOR OF THE ADVANCE</p>
      <p class="pt-2 font-bold underline">ACKNOWLEDGEMENT OF DEBT</p>
      <p class="pt-2">
        I, <span class="font-semibold">{{ form.claimantName || '…………………………' }}</span>, the undersigned, do hereby acknowledge that I am
        indebted to FoodBev Seta for the amount of
        <span class="font-semibold">{{ form.amount ? amountWords(form.amount) : '………………' }}</span>
      </p>
      <p class="pt-2">
        In the event that I fail to produce supporting documents/receipts within 3 days, I agree that this amount be deducted from my
        salary in full.
      </p>
      <p class="pt-2">I declare that the information supplied is to the best of my knowledge, true and correct.</p>
      <p class="pt-4">
        Claimant Signature………………………………………. …. Date: {{ fmtDate(form.claimantSignedAtUtc) }}
      </p>

      <p class="pt-6 font-bold underline">Approved</p>
      <p>
        Senior Manager: Finance
        <span class="font-semibold underline decoration-dotted">{{ form.financeManagerName || '……………………………………' }}</span>
        Date: {{ fmtDate(form.financeManagerSignedAtUtc) }}
      </p>
      <img
        v-if="sigSrc(form.financeManagerSignaturePngBase64)"
        :src="sigSrc(form.financeManagerSignaturePngBase64)!"
        alt="Finance senior manager signature"
        class="mt-2 h-14 max-w-[220px] object-contain"
      />
    </section>
  </article>
</template>

<style scoped>
.petty-cash-form-official {
  font-family: 'Times New Roman', Times, serif;
}
</style>
