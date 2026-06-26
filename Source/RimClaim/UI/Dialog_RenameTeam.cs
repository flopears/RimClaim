using System;
using UnityEngine;
using Verse;

namespace RimClaim
{
    public class Dialog_RenameTeam : Window
    {
        private string curName;
        private readonly Action<string> onConfirm;

        public override Vector2 InitialSize => new Vector2(280f, 130f);

        public Dialog_RenameTeam(string? existingName, int teamId, Action<string> onConfirm)
        {
            this.curName = existingName ?? "New Team";
            this.onConfirm = onConfirm;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnAccept = true;
            closeOnClickedOutside = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 24f), "RC_TeamNameLabel".Translate());

            string prev = curName;
            curName = Widgets.TextField(new Rect(0f, 30f, inRect.width, 30f), curName, 40);
            if (curName != prev)
                curName = curName.TrimStart();

            if (Widgets.ButtonText(new Rect(0f, inRect.height - 35f, inRect.width / 2f - 4f, 35f),
                "OK".Translate()) && curName.Length > 0)
            {
                onConfirm(curName);
                Close();
            }

            if (Widgets.ButtonText(new Rect(inRect.width / 2f + 4f, inRect.height - 35f,
                inRect.width / 2f - 4f, 35f), "Cancel".Translate()))
            {
                Close();
            }
        }

        public override void OnAcceptKeyPressed()
        {
            if (curName.Length > 0)
            {
                onConfirm(curName);
                base.OnAcceptKeyPressed();
            }
        }
    }
}
