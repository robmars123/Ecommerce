import { Link, type Href } from 'expo-router';
import { StyleSheet } from 'react-native';

type StubLinkProps = {
  href: Href;
  label: string;
};

export function StubLink({ href, label }: StubLinkProps) {
  return (
    <Link href={href} style={styles.link}>
      {label}
    </Link>
  );
}

const styles = StyleSheet.create({
  link: { minHeight: 44, paddingVertical: 12, fontSize: 16 },
});
