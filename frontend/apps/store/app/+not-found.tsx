import { ScreenStub } from '@/components/ScreenStub';
import { StubLink } from '@/components/StubLink';

export default function NotFoundScreen() {
  return (
    <ScreenStub title="Not found" detail="This screen does not exist.">
      <StubLink href="/" label="Go home" />
    </ScreenStub>
  );
}
