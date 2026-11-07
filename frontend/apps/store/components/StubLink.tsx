import { Link, type Href } from 'expo-router';
import { Text } from 'react-native';

import { touch } from '@/lib/design-tokens';

type StubLinkProps = {
  href: Href;
  label: string;
};

// The 44 touch target stays a token-sourced style: className does not resolve under Jest (arch §5.5 fallback).
export function StubLink({ href, label }: StubLinkProps) {
  return (
    <Link href={href} asChild>
      <Text className="py-md text-body text-foreground" style={{ minHeight: touch }}>
        {label}
      </Text>
    </Link>
  );
}
