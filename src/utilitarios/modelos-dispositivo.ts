/**
 * The models of the device, from the identifier the phone reports.
 *
 * The artwork is chosen by the camera layout, that is what tells the models
 * apart from each other. The name is what the panel shows under the device.
 */
export type PhoneArt =
  | "se"
  | "single"
  | "vertical"
  | "square"
  | "diagonal"
  | "island"
  | "tablet";

export type DeviceModel = {
  name: string;
  art: PhoneArt;
};

const IPHONES: Record<string, DeviceModel> = {
  // The iPhone X, the XS and the XS Max.
  "iPhone10,3": { name: "iPhone X", art: "vertical" },
  "iPhone10,6": { name: "iPhone X", art: "vertical" },
  "iPhone11,2": { name: "iPhone XS", art: "vertical" },
  "iPhone11,4": { name: "iPhone XS Max", art: "vertical" },
  "iPhone11,6": { name: "iPhone XS Max", art: "vertical" },
  // The XR and the 11.
  "iPhone11,8": { name: "iPhone XR", art: "single" },
  "iPhone12,1": { name: "iPhone 11", art: "single" },
  // The 11 Pro and the 11 Pro Max.
  "iPhone12,3": { name: "iPhone 11 Pro", art: "square" },
  "iPhone12,5": { name: "iPhone 11 Pro Max", art: "square" },
  // The SE of the second and the third generation.
  "iPhone12,8": { name: "iPhone SE (2ª geração)", art: "se" },
  "iPhone14,6": { name: "iPhone SE (3ª geração)", art: "se" },
  // The 12 and the 13.
  "iPhone13,1": { name: "iPhone 12 mini", art: "diagonal" },
  "iPhone13,2": { name: "iPhone 12", art: "diagonal" },
  "iPhone13,3": { name: "iPhone 12 Pro", art: "square" },
  "iPhone13,4": { name: "iPhone 12 Pro Max", art: "square" },
  "iPhone14,4": { name: "iPhone 13 mini", art: "diagonal" },
  "iPhone14,5": { name: "iPhone 13", art: "diagonal" },
  "iPhone14,2": { name: "iPhone 13 Pro", art: "square" },
  "iPhone14,3": { name: "iPhone 13 Pro Max", art: "square" },
  // The 14.
  "iPhone14,7": { name: "iPhone 14", art: "diagonal" },
  "iPhone14,8": { name: "iPhone 14 Plus", art: "diagonal" },
  "iPhone15,2": { name: "iPhone 14 Pro", art: "island" },
  "iPhone15,3": { name: "iPhone 14 Pro Max", art: "island" },
  // The 15.
  "iPhone15,4": { name: "iPhone 15", art: "island" },
  "iPhone15,5": { name: "iPhone 15 Plus", art: "island" },
  "iPhone16,1": { name: "iPhone 15 Pro", art: "island" },
  "iPhone16,2": { name: "iPhone 15 Pro Max", art: "island" },
  // The 16.
  "iPhone17,1": { name: "iPhone 16 Pro", art: "island" },
  "iPhone17,2": { name: "iPhone 16 Pro Max", art: "island" },
  "iPhone17,3": { name: "iPhone 16", art: "island" },
  "iPhone17,4": { name: "iPhone 16 Plus", art: "island" },
  "iPhone17,5": { name: "iPhone 16e", art: "single" },
  // The 17.
  "iPhone18,1": { name: "iPhone 17", art: "island" },
  "iPhone18,2": { name: "iPhone 17 Pro", art: "island" },
  "iPhone18,3": { name: "iPhone 17 Pro Max", art: "island" },
  // The 18.
  "iPhone19,1": { name: "iPhone 18", art: "island" },
  "iPhone19,2": { name: "iPhone 18 Pro", art: "island" },
  "iPhone19,3": { name: "iPhone 18 Pro Max", art: "island" },
  "iPhone19,4": { name: "iPhone 18 Plus", art: "island" },
};

const IPADS: Record<string, DeviceModel> = {
  "iPad8,1": { name: "iPad 10,2\"", art: "tablet" },
  "iPad8,2": { name: "iPad 10,5\"", art: "tablet" },
  "iPad8,3": { name: "iPad 10,2\"", art: "tablet" },
  "iPad8,4": { name: "iPad 10,5\"", art: "tablet" },
  "iPad8,5": { name: "iPad 10,2\"", art: "tablet" },
  "iPad8,6": { name: "iPad 10,5\"", art: "tablet" },
  "iPad8,7": { name: "iPad 10,2\"", art: "tablet" },
  "iPad8,8": { name: "iPad 10,5\"", art: "tablet" },
  "iPad8,9": { name: "iPad 10,2\"", art: "tablet" },
  "iPad8,10": { name: "iPad 10,5\"", art: "tablet" },
  "iPad11,1": { name: "iPad mini 5", art: "tablet" },
  "iPad11,2": { name: "iPad mini 5", art: "tablet" },
  "iPad11,3": { name: "iPad Air 10,5\"", art: "tablet" },
  "iPad11,4": { name: "iPad Air 10,5\"", art: "tablet" },
  "iPad11,6": { name: "iPad 8ª geração", art: "tablet" },
  "iPad11,7": { name: "iPad 8ª geração", art: "tablet" },
  "iPad12,1": { name: "iPad 9ª geração", art: "tablet" },
  "iPad12,2": { name: "iPad 9ª geração", art: "tablet" },
  "iPad13,1": { name: "iPad Air 4ª geração", art: "tablet" },
  "iPad13,2": { name: "iPad Air 4ª geração", art: "tablet" },
  "iPad13,4": { name: "iPad Pro 11\"", art: "tablet" },
  "iPad13,5": { name: "iPad Pro 11\"", art: "tablet" },
  "iPad13,6": { name: "iPad Pro 12,9\"", art: "tablet" },
  "iPad13,7": { name: "iPad Pro 12,9\"", art: "tablet" },
  "iPad13,8": { name: "iPad Pro 11\"", art: "tablet" },
  "iPad13,9": { name: "iPad Pro 11\"", art: "tablet" },
  "iPad13,10": { name: "iPad Pro 12,9\"", art: "tablet" },
  "iPhone13,16": { name: "iPad Air 5ª geração", art: "tablet" },
  "iPad13,17": { name: "iPad Air 5ª geração", art: "tablet" },
  "iPad14,1": { name: "iPad mini 6ª geração", art: "tablet" },
  "iPad14,2": { name: "iPad mini 6ª geração", art: "tablet" },
  "iPad14,3": { name: "iPad Pro 11\"", art: "tablet" },
  "iPad14,4": { name: "iPad Pro 11\"", art: "tablet" },
  "iPad14,5": { name: "iPad Pro 12,9\"", art: "tablet" },
  "iPad14,6": { name: "iPad Pro 12,9\"", art: "tablet" },
  "iPad14,8": { name: "iPad Air 11\"", art: "tablet" },
  "iPad14,9": { name: "iPad Air 11\"", art: "tablet" },
  "iPad14,10": { name: "iPad Air 11\"", art: "tablet" },
  "iPad14,11": { name: "iPad Air 11\"", art: "tablet" },
  "iPad16,3": { name: "iPad Pro 11\"", art: "tablet" },
  "iPad16,4": { name: "iPad Pro 11\"", art: "tablet" },
  "iPad16,5": { name: "iPad Pro 13\"", art: "tablet" },
  "iPad16,6": { name: "iPad Pro 13\"", art: "tablet" },
};

/** The artwork used when there is no device, or the model is unknown. */
export const DEFAULT_ART: PhoneArt = "island";

/**
 * The model of the device, from the identifier it reports.
 *
 * A model that is not in the table still gets an answer: the family is
 * guessed from the numbers of the identifier, so a phone that came out
 * after this build is not left without a picture.
 */
export const deviceModel = (productType: string | null | undefined): DeviceModel => {
  const type = (productType ?? "").trim();
  if (!type) {
    return { name: "", art: DEFAULT_ART };
  }

  const known = IPHONES[type] ?? IPADS[type];
  if (known) {
    return known;
  }

  // The iPad Air of the fifth generation reports as an iPhone.
  if (type.startsWith("iPhone13,16")) {
    return { name: "iPad Air 5ª geração", art: "tablet" };
  }

  const family = /^iPhone(\d+)/.exec(type);
  if (family) {
    const major = Number(family[1]);
    // From the 14 on the camera sits in a triangle, before that the 12 and
    // the 13 use the diagonal and the Pro models the square.
    if (major >= 14) {
      return { name: `iPhone ${major}`, art: "island" };
    }
    if (major >= 12) {
      return { name: `iPhone ${major}`, art: "diagonal" };
    }
    return { name: `iPhone ${major}`, art: "vertical" };
  }

  if (type.startsWith("iPad")) {
    return { name: "iPad", art: "tablet" };
  }

  return { name: "", art: DEFAULT_ART };
};
