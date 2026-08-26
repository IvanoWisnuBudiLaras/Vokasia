export function RichTextContent({ value, className }: { value: string; className?: string }) {
  if (!value) return null;

  const isHtml = /<[a-z][\s\S]*>/i.test(value);
  if (!isHtml) {
    return <p className={className}>{value}</p>;
  }

  return (
    <div
      className={className ? `prose max-w-none ${className}` : "prose max-w-none"}
      dangerouslySetInnerHTML={{ __html: value }}
    />
  );
}
