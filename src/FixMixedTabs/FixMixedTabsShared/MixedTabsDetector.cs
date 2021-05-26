using Microsoft.VisualStudio.Text;

namespace FixMixedTabs
{
    public static class MixedTabsDetector
    {
        public static bool HasMixedTabsAndSpaces(int tabSize, ITextSnapshot snapshot)
        {
            bool startsWithSpaces = false;
            bool startsWithTabs = false;

            foreach (var line in snapshot.Lines)
            {
                if (line.Length > 0)
                {
                    char firstChar = line.Start.GetChar();
                    if (firstChar == '\t')
                        startsWithTabs = true;
                    else if (firstChar == ' ')
                    {
                        // We need to count to make sure there are enough spaces to go into a tab or a tab that follows the spaces.
                        int countOfSpaces = 1;
                        for (int i = line.Start.Position + 1; i < line.End.Position; i++)
                        {
                            char ch = snapshot[i];
                            if (ch == ' ')
                            {
                                countOfSpaces++;
                                if (countOfSpaces >= tabSize)
                                {
                                    startsWithSpaces = true;
                                    break;
                                }
                            }
                            else if (ch == '\t')
                            {
                                startsWithSpaces = true;
                                break;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }

                    if (startsWithSpaces && startsWithTabs)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
