// /*
//     Copyright (C) 2021 0x90d
//     This file is part of VideoDuplicateFinder
//     VideoDuplicateFinder is free software: you can redistribute it and/or modify
//     it under the terms of the GPLv3 as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//     VideoDuplicateFinder is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//     You should have received a copy of the GNU General Public License
//     along with VideoDuplicateFinder.  If not, see <http://www.gnu.org/licenses/>.
// */
//

using System.Reactive;
using System.Reflection;
using System.Text;
using ReactiveUI;
using VDF.Core.ViewModels;
using VDF.GUI.Views;

namespace VDF.GUI.ViewModels {
	public sealed class ExpressionBuilderVM : ReactiveObject {
		public ExpressionBuilder host;
		public ExpressionBuilderVM(ExpressionBuilder Host) {
			host = Host;
			var sb = new StringBuilder();

			var properties = typeof(DuplicateItem).GetProperties(BindingFlags.Public | BindingFlags.Instance);
			foreach (var p in properties) {
				sb.AppendLine($"arg.{nameof(DuplicateItemVM.ItemInfo)}.{p.Name} ({p.PropertyType.Name})");
			}
			sb.AppendLine();
			sb.AppendLine();
			sb.AppendLine($"Example: arg.{nameof(DuplicateItemVM.ItemInfo)}.{nameof(DuplicateItem.IsImage)} && arg.{nameof(DuplicateItemVM.ItemInfo)}.{nameof(DuplicateItem.SizeLong)} > 3000");
			sb.AppendLine($"Example: arg.{nameof(DuplicateItemVM.ItemInfo)}.{nameof(DuplicateItem.Path)}.Contains(\"imageFolder\")");
			sb.AppendLine($"Example: arg.{nameof(DuplicateItemVM.ItemInfo)}.{nameof(DuplicateItem.Duration)}.Minute > 15");
			sb.AppendLine("Note: Expression uses 'Dynamic Expresso' library which understands C# syntax");

			AvailableProperties = sb.ToString();
		}
		string _ExpressionText = string.Empty;
		public string ExpressionText {
			get => _ExpressionText;
			set => this.RaiseAndSetIfChanged(ref _ExpressionText, value);
		}
		public string AvailableProperties { get; }
		public ReactiveCommand<Unit, Unit> CancelCommand => ReactiveCommand.Create(() => {
			host.Close(false);
		});
		public ReactiveCommand<Unit, Unit> OKCommand => ReactiveCommand.Create(() => {
			host.Close(true);
		});
	}
}
