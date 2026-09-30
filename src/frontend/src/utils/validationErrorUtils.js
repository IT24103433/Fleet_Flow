const normalizeFieldName = (name) => {
  const leaf = String(name || '').split('.').at(-1).replace(/\[(\d+)\]/g, '$1');
  return leaf ? `${leaf.charAt(0).toLowerCase()}${leaf.slice(1)}` : '';
};

export function normalizeValidationErrors(errors) {
  if (!errors || typeof errors !== 'object' || Array.isArray(errors)) return {};

  return Object.entries(errors).reduce((normalized, [field, value]) => {
    const fieldName = normalizeFieldName(field);
    const messages = (Array.isArray(value) ? value : [value])
      .filter(message => typeof message === 'string' && message.trim())
      .map(message => message.trim());
    if (fieldName && messages.length > 0) normalized[fieldName] = messages.join(' ');
    return normalized;
  }, {});
}
