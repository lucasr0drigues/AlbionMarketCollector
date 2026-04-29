export type LocationRole = 'royal' | 'rest' | 'caerleon' | 'brecilien' | 'smugglers-den' | 'black-market';

export interface LocationOption {
  key: string;
  label: string;
  shortLabel: string;
  ids: string[];
  role: LocationRole;
}

const SMUGGLERS_DEN_IDS = [
  'BLACKBANK-2310', 'BLACKBANK-0321', 'BLACKBANK-0307', 'BLACKBANK-4322',
  'BLACKBANK-2336', 'BLACKBANK-0320', 'BLACKBANK-0341', 'BLACKBANK-0344',
  'BLACKBANK-0349', 'BLACKBANK-0353', 'BLACKBANK-1312', 'BLACKBANK-1323',
  'BLACKBANK-1339', 'BLACKBANK-1342', 'BLACKBANK-1343', 'BLACKBANK-1348',
  'BLACKBANK-1359', 'BLACKBANK-2308', 'BLACKBANK-2311', 'BLACKBANK-2333',
  'BLACKBANK-2342', 'BLACKBANK-2344', 'BLACKBANK-2347', 'BLACKBANK-2348',
  'BLACKBANK-3306', 'BLACKBANK-3344', 'BLACKBANK-3345', 'BLACKBANK-3351',
  'BLACKBANK-3355', 'BLACKBANK-3357', 'BLACKBANK-4313', 'BLACKBANK-4318',
  'BLACKBANK-4345', 'BLACKBANK-4351', 'BLACKBANK-4357',
];

const BASE: LocationOption[] = [
  { key: 'fort-sterling', label: 'Fort Sterling', shortLabel: 'FS', ids: ['4002', '4301'], role: 'royal' },
  { key: 'lymhurst',      label: 'Lymhurst',      shortLabel: 'LY', ids: ['1002', '1301'], role: 'royal' },
  { key: 'bridgewatch',   label: 'Bridgewatch',   shortLabel: 'BW', ids: ['2004', '2301'], role: 'royal' },
  { key: 'martlock',      label: 'Martlock',      shortLabel: 'ML', ids: ['3008', '3301'], role: 'royal' },
  { key: 'thetford',      label: 'Thetford',      shortLabel: 'TF', ids: ['0007', '0301'], role: 'royal' },
  { key: 'caerleon',      label: 'Caerleon',      shortLabel: 'CA', ids: ['3005', '3003'], role: 'caerleon' },
  { key: 'brecilien',     label: 'Brecilien',     shortLabel: 'BR', ids: ['5003'],         role: 'brecilien' },
  { key: 'arthurs-rest',  label: "Arthur's Rest", shortLabel: 'AR', ids: ['4300'],         role: 'rest' },
  { key: 'merlyns-rest',  label: "Merlyn's Rest", shortLabel: 'MR', ids: ['1012'],         role: 'rest' },
  { key: 'morganas-rest', label: "Morgana's Rest",shortLabel: 'MO', ids: ['0008'],         role: 'rest' },
];

export const SOURCE_LOCATIONS: LocationOption[] = [
  ...BASE,
  { key: 'smugglers-den', label: "Smuggler's Den", shortLabel: 'SD', ids: SMUGGLERS_DEN_IDS, role: 'smugglers-den' },
];

export const SELLING_LOCATIONS: LocationOption[] = [
  ...BASE,
  { key: 'black-market', label: 'Black Market', shortLabel: 'BM', ids: ['3003'], role: 'black-market' },
];

export const DEFAULT_SOURCE_KEYS = ['fort-sterling'];
export const DEFAULT_SELLING_KEYS = ['black-market'];

export function flattenLocationIds(options: LocationOption[], selectedKeys: readonly string[]): string[] {
  return options
    .filter((option) => selectedKeys.includes(option.key))
    .flatMap((option) => option.ids);
}
