import React from 'react';

const paths = {
  search: <><circle cx="10.5" cy="10.5" r="6.5" /><path d="m16 16 5 5" /></>,
  cart: <><path d="M2 3h3l3 12h10l3-9H6M9 19h.01M18 19h.01" /><circle cx="9" cy="20" r="1" /><circle cx="18" cy="20" r="1" /></>,
  user: <><circle cx="12" cy="12" r="10" /><circle cx="12" cy="8" r="3" /><path d="M5 20v-2a7 7 0 0 1 14 0v2" /></>,
  leaf: <><path d="M20 3C6 2 2 10 7 17c7 5 15 0 13-14Z" /><path d="M3 22 16 8" /></>,
  grid: <><rect x="3" y="3" width="7" height="7" rx="1" /><rect x="14" y="3" width="7" height="7" rx="1" /><rect x="3" y="14" width="7" height="7" rx="1" /><rect x="14" y="14" width="7" height="7" rx="1" /></>,
  heart: <path d="M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1.1-1.1a5.5 5.5 0 0 0-7.8 7.8L12 21l8.8-8.6a5.5 5.5 0 0 0 0-7.8Z" />,
  pin: <><path d="M19 10c0 5-7 12-7 12S5 15 5 10a7 7 0 1 1 14 0Z" /><circle cx="12" cy="10" r="2" /></>,
  check: <path d="m4 12 5 5L20 6" />,
  shield: <><path d="m12 2 9 4v6c0 6-9 10-9 10S3 18 3 12V6Z" /><path d="m8 12 3 3 5-6" /></>,
  truck: <><path d="M2 4h12v13H2ZM14 9h5l3 4v4h-8" /><circle cx="6" cy="18" r="2" /><circle cx="18" cy="18" r="2" /></>,
  card: <><rect x="2" y="4" width="20" height="16" rx="2" /><path d="M2 9h20M6 15h4" /></>,
  share: <><circle cx="18" cy="4" r="3" /><circle cx="5" cy="12" r="3" /><circle cx="18" cy="20" r="3" /><path d="m8 11 7-5M8 13l7 5" /></>,
  eye: <><path d="M2 12S6 5 12 5s10 7 10 7-4 7-10 7S2 12 2 12Z" /><circle cx="12" cy="12" r="3" /></>,
  arrow: <path d="M3 12h18m-6-6 6 6-6 6" />,
  home: <><path d="m2 11 10-9 10 9M5 9v13h14V9M10 22v-8h4v8" /></>,
  filter: <><path d="M2 4h20M5 10h14M9 16h6M11 21h2" /></>,
};

export function Icon({ name, size = 22, ...props }) {
  return <svg width={size} height={size} viewBox="0 0 24 24" fill="none"
    stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round"
    aria-hidden="true" {...props}>{paths[name] || paths.leaf}</svg>;
}

export function BrandMark() {
  return <svg viewBox="0 0 68 58" fill="currentColor" aria-hidden="true">
    <path d="M32 53C30 26 44 9 64 3c1 27-11 39-27 40 5-13 10-21 17-29-13 11-19 24-20 39ZM29 46C10 44 4 31 4 17c18 3 28 13 25 26-6-9-11-14-17-18 9 10 14 19 17 27Z" />
  </svg>;
}

export function BotanicalLeaves({ right = false }) {
  return <svg className={`market-botanical ${right ? 'market-botanical-right' : ''}`}
    viewBox="0 0 200 370" aria-hidden="true">
    <path d="M10 370Q60 240 110 30M36 300l99-68M60 214l-26-82M80 149l83-56"
      fill="none" stroke="#a4be81" strokeWidth="3" />
    {[[105, 24, -20], [42, 111, -55], [140, 104, 30], [74, 189, -55],
      [127, 233, 30], [26, 279, -40], [83, 322, 50]].map(([x, y, r]) =>
      <path key={y} d={`M${x} ${y}q-43 24-4 91q49-43 4-91Z`}
        fill="#b8ce8f" opacity=".52" transform={`rotate(${r} ${x} ${y})`} />)}
  </svg>;
}
