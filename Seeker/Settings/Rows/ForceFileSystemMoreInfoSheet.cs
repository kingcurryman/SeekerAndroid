/*
 * Copyright (c) 2026 kingcurryman
 * Modified: Updated the GitHub build link for Song Seeker.
 *
 * This file is part of Seeker
 *
 * Seeker is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Seeker is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with Seeker. If not, see <http://www.gnu.org/licenses/>.
 */

using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using Google.Android.Material.BottomSheet;

namespace Seeker.Settings.Rows
{
    public sealed class ForceFileSystemMoreInfoSheet : BottomSheetDialogFragment
    {
        private ISettingsHost _host;

        public static void Show(ISettingsHost host)
        {
            var sheet = new ForceFileSystemMoreInfoSheet { _host = host };
            sheet.Show(((AndroidX.AppCompat.App.AppCompatActivity)host.Activity).SupportFragmentManager, "ForceFileSystemMoreInfo");
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate(Resource.Layout.force_filesystem_more_info_sheet, container, false);

            view.FindViewById<Button>(Resource.Id.get_izzydroid_build).Click += (s, e) =>
            {
                Dismiss();
                CommonHelpers.ViewUri(Android.Net.Uri.Parse("https://apt.izzysoft.de/fdroid/index/apk/com.companyname.andriodapp1"), this.Context);
            };

            view.FindViewById<Button>(Resource.Id.get_github_build).Click += (s, e) =>
            {
                Dismiss();
                CommonHelpers.ViewUri(Android.Net.Uri.Parse("https://github.com/kingcurryman/SeekerAndroid/actions/workflows/android-debug.yml"), this.Context);
            };

            return view;
        }
    }
}
