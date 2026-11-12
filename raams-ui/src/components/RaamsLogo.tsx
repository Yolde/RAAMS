export function RaamsLogo({ size = 40 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 48 48" fill="none" aria-hidden="true" focusable="false">
      <path d="M24 3 6 9v12c0 11.5 7.3 19.1 18 24 10.7-4.9 18-12.5 18-24V9L24 3Z"
        fill="#FFFFFF" stroke="#F76B38" strokeWidth="2" strokeLinejoin="round" />
      <path d="M24 7 10 11.7V21c0 9.1 5.6 15.4 14 19.6 8.4-4.2 14-10.5 14-19.6v-9.3L24 7Z"
        fill="#02B702" />
      <polyline points="16 23 21.5 28.5 32 17.5"
        stroke="#FFD400" strokeWidth="4" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}
