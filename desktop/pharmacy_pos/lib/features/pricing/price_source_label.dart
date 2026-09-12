String priceSourceLabel(String? source) => switch (source) {
  'PriceLevel' => 'Price Level',
  'QuantityBreak' => 'Quantity Break',
  'PricingRule' => 'Pricing Rule',
  'Promotion' => 'Promotion',
  'ManualOverride' => 'Manual Override',
  'DocumentSnapshot' => 'Document Snapshot',
  _ => 'Retail',
};
