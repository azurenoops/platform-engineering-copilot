/**
 * Network Validation Utilities
 * Provides CIDR validation, subnet overlap detection, and network configuration validation
 */

export interface ValidationResult {
  valid: boolean;
  errors: string[];
}

/**
 * Validates CIDR notation format
 */
export function isValidCIDR(cidr: string): boolean {
  if (!cidr) return false;
  
  const cidrRegex = /^(\d{1,3}\.){3}\d{1,3}\/\d{1,2}$/;
  if (!cidrRegex.test(cidr)) return false;
  
  const [ip, prefix] = cidr.split('/');
  const prefixNum = parseInt(prefix);
  
  // Check prefix is valid (0-32)
  if (prefixNum < 0 || prefixNum > 32) return false;
  
  // Check IP octets are valid (0-255)
  const octets = ip.split('.').map(Number);
  return octets.every(octet => octet >= 0 && octet <= 255);
}

/**
 * Converts CIDR to IP range
 */
function cidrToRange(cidr: string): { start: number; end: number } {
  const [ip, prefix] = cidr.split('/');
  const prefixNum = parseInt(prefix);
  const octets = ip.split('.').map(Number);
  
  // Convert IP to 32-bit integer
  const ipInt = (octets[0] << 24) | (octets[1] << 16) | (octets[2] << 8) | octets[3];
  
  // Calculate network mask
  const mask = (0xFFFFFFFF << (32 - prefixNum)) >>> 0;
  
  // Calculate start and end IPs
  const start = (ipInt & mask) >>> 0;
  const end = (start | (~mask >>> 0)) >>> 0;
  
  return { start, end };
}

/**
 * Checks if CIDR ranges overlap
 */
export function cidrsOverlap(cidr1: string, cidr2: string): boolean {
  if (!isValidCIDR(cidr1) || !isValidCIDR(cidr2)) return false;
  
  const range1 = cidrToRange(cidr1);
  const range2 = cidrToRange(cidr2);
  
  // Check if ranges overlap
  return (range1.start <= range2.end && range2.start <= range1.end);
}

/**
 * Checks if subnet CIDR is within VNet CIDR
 */
export function isSubnetWithinVNet(subnetCidr: string, vnetCidr: string): boolean {
  if (!isValidCIDR(subnetCidr) || !isValidCIDR(vnetCidr)) return false;
  
  const subnetRange = cidrToRange(subnetCidr);
  const vnetRange = cidrToRange(vnetCidr);
  
  // Subnet must be completely within VNet
  return subnetRange.start >= vnetRange.start && subnetRange.end <= vnetRange.end;
}

/**
 * Calculates the next available CIDR for a subnet
 */
export function calculateNextSubnetCIDR(
  vnetCidr: string,
  existingSubnets: string[],
  desiredSize: number = 24 // /24 by default (256 IPs)
): string | null {
  if (!isValidCIDR(vnetCidr)) return null;
  
  const vnetRange = cidrToRange(vnetCidr);
  const [vnetIp, vnetPrefix] = vnetCidr.split('/');
  const vnetPrefixNum = parseInt(vnetPrefix);
  
  // Calculate subnet size in IPs
  const subnetSize = Math.pow(2, 32 - desiredSize);
  
  // Sort existing subnets by start IP
  const occupiedRanges = existingSubnets
    .filter(isValidCIDR)
    .map(cidr => cidrToRange(cidr))
    .sort((a, b) => a.start - b.start);
  
  // Try to find a gap
  let candidateStart = vnetRange.start;
  
  for (const occupied of occupiedRanges) {
    if (candidateStart + subnetSize <= occupied.start) {
      // Found a gap before this subnet
      break;
    }
    // Move candidate past this occupied range
    candidateStart = occupied.end + 1;
  }
  
  // Check if candidate fits within VNet
  const candidateEnd = candidateStart + subnetSize - 1;
  if (candidateEnd > vnetRange.end) {
    return null; // No space left
  }
  
  // Convert back to CIDR notation
  const octet1 = (candidateStart >>> 24) & 0xFF;
  const octet2 = (candidateStart >>> 16) & 0xFF;
  const octet3 = (candidateStart >>> 8) & 0xFF;
  const octet4 = candidateStart & 0xFF;
  
  return `${octet1}.${octet2}.${octet3}.${octet4}/${desiredSize}`;
}

/**
 * Validates multiple subnets for overlaps and VNet containment
 */
export function validateSubnets(
  subnets: Array<{ name: string; addressPrefix: string }>,
  vnetCidr: string
): ValidationResult {
  const errors: string[] = [];
  
  // Check each subnet is valid and within VNet
  subnets.forEach((subnet, index) => {
    if (!subnet.addressPrefix) {
      errors.push(`Subnet ${index + 1} (${subnet.name}): Address prefix is required`);
      return;
    }
    
    if (!isValidCIDR(subnet.addressPrefix)) {
      errors.push(`Subnet ${index + 1} (${subnet.name}): Invalid CIDR format`);
      return;
    }
    
    if (vnetCidr && !isSubnetWithinVNet(subnet.addressPrefix, vnetCidr)) {
      errors.push(`Subnet ${index + 1} (${subnet.name}): CIDR ${subnet.addressPrefix} is not within VNet address space ${vnetCidr}`);
    }
  });
  
  // Check for overlaps
  for (let i = 0; i < subnets.length; i++) {
    for (let j = i + 1; j < subnets.length; j++) {
      if (subnets[i].addressPrefix && subnets[j].addressPrefix &&
          cidrsOverlap(subnets[i].addressPrefix, subnets[j].addressPrefix)) {
        errors.push(`Subnets ${i + 1} (${subnets[i].name}) and ${j + 1} (${subnets[j].name}) have overlapping address ranges`);
      }
    }
  }
  
  // Check for duplicate names
  const names = subnets.map(s => s.name.toLowerCase()).filter(n => n);
  const duplicateNames = names.filter((name, index) => names.indexOf(name) !== index);
  if (duplicateNames.length > 0) {
    const uniqueDuplicates = Array.from(new Set(duplicateNames));
    errors.push(`Duplicate subnet names found: ${uniqueDuplicates.join(', ')}`);
  }
  
  return {
    valid: errors.length === 0,
    errors
  };
}

/**
 * Validates VNet name format
 */
export function isValidVNetName(name: string): boolean {
  if (!name) return false;
  if (name.length < 2 || name.length > 64) return false;
  
  // Must start with letter or number
  if (!/^[a-zA-Z0-9]/.test(name)) return false;
  
  // Can contain letters, numbers, hyphens, underscores, periods
  if (!/^[a-zA-Z0-9-_.]+$/.test(name)) return false;
  
  // Must end with letter or number
  if (!/[a-zA-Z0-9]$/.test(name)) return false;
  
  return true;
}

/**
 * Validates subnet name format
 */
export function isValidSubnetName(name: string): boolean {
  if (!name) return false;
  if (name.length < 1 || name.length > 80) return false;
  
  // Can contain letters, numbers, hyphens, underscores, periods
  if (!/^[a-zA-Z0-9-_.]+$/.test(name)) return false;
  
  return true;
}

/**
 * Gets the size of a subnet in number of IP addresses
 */
export function getSubnetSize(cidr: string): number | null {
  if (!isValidCIDR(cidr)) return null;
  
  const prefix = parseInt(cidr.split('/')[1]);
  return Math.pow(2, 32 - prefix);
}

/**
 * Checks if subnet is large enough for a specific purpose
 */
export function isSubnetSizeSufficient(cidr: string, purpose: string): boolean {
  const size = getSubnetSize(cidr);
  if (!size) return false;
  
  // Minimum sizes for different purposes
  const minimumSizes: { [key: string]: number } = {
    'Application': 16, // /28 minimum
    'PrivateEndpoints': 16, // /28 minimum
    'ApplicationGateway': 16, // /28 minimum
    'Database': 16, // /28 minimum
    'ContainerApps': 512, // /23 minimum (Microsoft requirement)
  };
  
  const minSize = minimumSizes[purpose] || 16;
  return size >= minSize;
}

/**
 * Formats CIDR validation error messages
 */
export function getSubnetSizeRecommendation(purpose: string): string {
  const recommendations: { [key: string]: string } = {
    'Application': 'Recommended: /26 (64 IPs) or larger',
    'PrivateEndpoints': 'Recommended: /28 (16 IPs) or larger',
    'ApplicationGateway': 'Recommended: /26 (64 IPs) or larger',
    'Database': 'Recommended: /27 (32 IPs) or larger',
    'ContainerApps': 'Required: /23 (512 IPs) or larger (Microsoft requirement)',
  };
  
  return recommendations[purpose] || 'Recommended: /28 (16 IPs) or larger';
}
