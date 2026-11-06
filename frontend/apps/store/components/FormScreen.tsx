import type { ReactNode } from 'react';
import { ScrollView, StyleSheet } from 'react-native';

import { ScreenTitle } from './ScreenTitle';

/** A titled, scrollable page for forms. */
export function FormScreen({ title, children }: { title: string; children: ReactNode }) {
  return (
    <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
      <ScreenTitle title={title} />
      {children}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { padding: 16, gap: 12 },
});
